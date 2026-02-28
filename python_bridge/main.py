import sys
import os
import asyncio
import io
from typing import Optional, List
import json

# Force UTF-8 for standard streams
if sys.stdout.encoding != 'utf-8':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
if sys.stderr.encoding != 'utf-8':
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

# Add current directory to path so browser_use can be imported
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import yt_dlp
from browser_use import Agent
from browser_use.llm.openai.chat import ChatOpenAI
from browser_use.llm.deepseek.chat import ChatDeepSeek
from browser_use.llm.volcengine.chat import ChatVolcengine
from browser_use.browser import BrowserSession as Browser

app = FastAPI()

# Track running agent tasks to allow cancellation
running_agents = {}

class AgentRequest(BaseModel):
    task: str
    api_key: str
    base_url: str
    model: str

@app.post("/agent/stop")
async def stop_agent():
    """
    Stop all currently running agents.
    """
    print("Received stop request. Cancelling all running agents...")
    count = 0
    for task_id, task in list(running_agents.items()):
        if not task.done():
            task.cancel()
            count += 1
    return {"status": "success", "cancelled_count": count}

@app.post("/agent/run")
async def run_agent_endpoint(request: AgentRequest):
    """
    Wrapper to handle task management for the agent.
    """
    task_id = f"agent_{id(request)}_{asyncio.get_event_loop().time()}"
    loop = asyncio.get_running_loop()
    
    # Create the actual work task
    work_task = loop.create_task(do_run_agent(request))
    running_agents[task_id] = work_task
    
    try:
        result = await work_task
        return result
    except asyncio.CancelledError:
        print(f"Agent task {task_id} was cancelled.")
        return {
            "status": "cancelled",
            "result": "任务已被用户手动停止。",
            "is_successful": False
        }
    finally:
        # Clean up
        if task_id in running_agents:
            del running_agents[task_id]

async def do_run_agent(request: AgentRequest):
    """
    Run the browser-use agent with the provided task and LLM configuration.
    Connects to the local WebView2 instance via CDP (port 9222).
    """
    print(f"Received task: {request.task}")
    print(f"Model: {request.model}")
    print(f"Base URL: {request.base_url}")
    try:
        # 1. Initialize LLM
        # Handle cases where base_url might be empty or specific to a provider
        base_url = request.base_url if request.base_url else None
        
        # Determine which LLM provider to use
        if base_url and ("volces.com" in base_url or "volcengine" in base_url):
            llm = ChatVolcengine(
                api_key=request.api_key,
                base_url=base_url,
                model=request.model,
                temperature=0.0,
            )
        elif base_url and "deepseek" in base_url:
            llm = ChatDeepSeek(
                api_key=request.api_key,
                base_url=base_url,
                model=request.model,
                temperature=0.0,
            )
        else:
            llm = ChatOpenAI(
                api_key=request.api_key,
                base_url=base_url,
                model=request.model,
                temperature=0.0, # Agents work best with low temperature
            )

        # 2. Initialize Browser connecting to existing CDP
        browser = Browser(cdp_url="http://localhost:9222")

        # 3. Initialize Agent
        use_thinking = "flash" not in request.model.lower() and "thinking" not in request.model.lower()
        
        agent = Agent(
            task=request.task,
            llm=llm,
            browser=browser,
            directly_open_url=True, # Enable direct navigation to save steps
            use_thinking=use_thinking,
            max_actions_per_step=10, # Allow more actions per step to reduce overhead
        )

        # 4. Run Agent and return result
        history = await agent.run()
        
        # 5. Extract and Format result
        # Try multiple ways to get the final result text
        result_text = history.final_result()

        # If no explicit final_result, check for errors
        errors = history.errors()
        error_msg = ""
        if errors:
            error_msg = "\n".join([str(e) for e in errors if e is not None])
            print(f"Agent Errors: {error_msg}")
        
        # If no explicit final_result, look for extracted content in history
        if not result_text:
            contents = history.extracted_content()
            if contents:
                result_text = contents[-1]
                
        # If still nothing, check last model actions (maybe it just finished)
        if not result_text:
            actions = history.model_actions()
            if actions:
                # Look for a 'done' action or similar that might have text
                for action in reversed(actions):
                    if 'done' in str(action): # Check action as string since it might be a pydantic object
                        try:
                            # Try to get text if it's a dict or object
                            if isinstance(action, dict):
                                result_text = action.get('done', {}).get('text')
                            else:
                                result_text = getattr(action.done, 'text', None)
                        except:
                            pass
                        if result_text: break
        
        if not result_text:
            if error_msg:
                result_text = f"任务执行失败，错误信息: {error_msg}"
            else:
                result_text = "任务已执行完成，但未返回具体文本结果。请检查浏览器页面是否已达到预期状态。"

        base_success = history.is_successful()
        has_errors = bool(errors)
        is_successful = bool(base_success) and not has_errors

        return {
            "status": "success" if is_successful else "failed",
            "result": result_text,
            "steps": len(history.history),
            "is_successful": is_successful,
            "errors": errors
        }
    
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))

class VideoDownloadRequest(BaseModel):
    url: str
    save_path: Optional[str] = None
    format_id: Optional[str] = None

@app.post("/video/info")
async def get_video_info(request: VideoDownloadRequest):
    """
    Get video info using yt-dlp from the provided URL.
    """
    print(f"Received video info request for URL: {request.url}")
    try:
        ydl_opts = {
            'quiet': True,
            'no_warnings': True,
        }
        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            loop = asyncio.get_event_loop()
            info = await loop.run_in_executor(None, lambda: ydl.extract_info(request.url, download=False))
            
            formats = []
            if 'formats' in info:
                for f in info['formats']:
                    # 只保留有视频和音频的格式，或者至少有视频的
                    if f.get('vcodec') != 'none':
                        formats.append({
                            'format_id': f.get('format_id'),
                            'ext': f.get('ext'),
                            'resolution': f.get('resolution') or f"{f.get('width')}x{f.get('height')}",
                            'filesize': f.get('filesize'),
                            'note': f.get('format_note') or f.get('format'),
                            'vcodec': f.get('vcodec'),
                            'acodec': f.get('acodec'),
                        })
            
            return {
                "status": "success",
                "title": info.get('title'),
                "thumbnail": info.get('thumbnail'),
                "duration": info.get('duration'),
                "formats": formats
            }
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/video/download")
async def download_video(request: VideoDownloadRequest):
    """
    Download video using yt-dlp from the provided URL.
    """
    if not request.save_path:
        raise HTTPException(status_code=400, detail="save_path is required for download")
        
    print(f"Received video download request for URL: {request.url}, format_id: {request.format_id}")
    try:
        # 确保保存路径存在
        if not os.path.exists(request.save_path):
            os.makedirs(request.save_path)

        ydl_opts = {
            'outtmpl': os.path.join(request.save_path, '%(title)s.%(ext)s'),
            'format': request.format_id if request.format_id else 'best', # 优先下载最佳质量
            'quiet': True,
            'no_warnings': True,
        }

        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            loop = asyncio.get_event_loop()
            await loop.run_in_executor(None, lambda: ydl.download([request.url]))
            
        return {"status": "success", "message": "视频下载成功"}
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    import uvicorn
    print("Starting Browser-Use Bridge on port 8000...")
    print("Ensure your C# Browser is running with --remote-debugging-port=9222")
    uvicorn.run(app, host="127.0.0.1", port=8000)
