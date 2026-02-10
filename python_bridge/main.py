import sys
import os
import asyncio
from typing import Optional, List
import json

# Add current directory to path so browser_use can be imported
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from browser_use import Agent
from browser_use.llm.openai.chat import ChatOpenAI
from browser_use.browser import BrowserSession as Browser

app = FastAPI()

class AgentRequest(BaseModel):
    task: str
    api_key: str
    base_url: str
    model: str

@app.post("/agent/run")
async def run_agent(request: AgentRequest):
    """
    Run the browser-use agent with the provided task and LLM configuration.
    Connects to the local WebView2 instance via CDP (port 9222).
    """
    print(f"Received task: {request.task}")
    print(f"Model: {request.model}")
    try:
        # 1. Initialize LLM
        # Handle cases where base_url might be empty or specific to a provider
        base_url = request.base_url if request.base_url else None
        
        llm = ChatOpenAI(
            api_key=request.api_key,
            base_url=base_url,
            model=request.model,
            temperature=0.0, # Agents work best with low temperature
        )

        # 2. Initialize Browser connecting to existing CDP
        browser = Browser(cdp_url="http://localhost:9222")

        # 3. Initialize Agent
        agent = Agent(
            task=request.task,
            llm=llm,
            browser=browser,
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

if __name__ == "__main__":
    import uvicorn
    print("Starting Browser-Use Bridge on port 8000...")
    print("Ensure your C# Browser is running with --remote-debugging-port=9222")
    uvicorn.run(app, host="127.0.0.1", port=8000)
