// Content Script for KunQiong AI Assistant
// Inject the helper into the Main World so the Host (C#) can call it via ExecuteScriptAsync
(function() {
    const script = document.createElement('script');
    script.textContent = `
    (function() {
        if (window.__AiAssistant) return;

        window.__AiAssistant = {
            // 点击元素
            click: function(selector) {
                try {
                    let el;
                    if (/^\\d+$/.test(selector)) {
                        el = document.querySelector(\`[data-ai-id="\${selector}"]\`);
                    } else {
                        el = document.querySelector(selector);
                    }

                    if (el) {
                        el.click();
                        return { success: true, message: "Clicked" };
                    }
                    return { success: false, message: "Element not found" };
                } catch (e) {
                    return { success: false, message: e.message };
                }
            },

            // 输入文本
            type: function(selector, text) {
                try {
                    let el;
                    if (/^\\d+$/.test(selector)) {
                        el = document.querySelector(\`[data-ai-id="\${selector}"]\`);
                    } else {
                        el = document.querySelector(selector);
                    }

                    if (el) {
                        el.focus();
                        el.value = text;
                        el.dispatchEvent(new Event('input', { bubbles: true }));
                        el.dispatchEvent(new Event('change', { bubbles: true }));
                        
                        // 尝试触发 React/Vue 等框架的事件绑定
                        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value").set;
                        if (nativeInputValueSetter) {
                            nativeInputValueSetter.call(el, text);
                            el.dispatchEvent(new Event('input', { bubbles: true }));
                        }
                        return { success: true, message: "Typed" };
                    }
                    return { success: false, message: "Element not found" };
                } catch (e) {
                    return { success: false, message: e.message };
                }
            },

            // 读取页面内容
            readPage: function() {
                try {
                    // 1. 移除干扰元素
                    // 注意：这里操作的是页面的真实 DOM，但为了不破坏显示，我们操作 clone
                    // 但是 clone 不包含事件状态。为了分配 ID，我们需要操作真实 DOM?
                    // 不，我们只给真实 DOM 加属性，不加可见元素。
                    
                    // 为了性能，我们直接遍历
                    let interactableCount = 0;
                    const interactables = document.querySelectorAll('a, button, input, select, textarea, [role="button"]');
                    interactables.forEach(el => {
                        interactableCount++;
                        el.setAttribute('data-ai-id', interactableCount);
                    });

                    // 使用 clone 提取文本
                    const clone = document.body.cloneNode(true);
                    const toRemove = clone.querySelectorAll('script, style, noscript, iframe, svg, meta, link');
                    toRemove.forEach(el => el.remove());
                    
                    // 在 clone 中插入 ID 标记以便 LLM 识别
                    const cloneInteractables = clone.querySelectorAll('a, button, input, select, textarea, [role="button"]');
                    // 注意：clone 的顺序应该和原版一致
                    let cloneCount = 0;
                    cloneInteractables.forEach(el => {
                        cloneCount++;
                        const mark = document.createElement('span');
                        mark.textContent = \` [ID:\${cloneCount}] \`;
                        mark.style.display = 'none'; // 虽然 clone 不显示，但 innerText 会包含隐藏元素吗？
                        // innerText 不包含 display:none 的元素。textContent 包含。
                        // 我们希望 LLM 看到 ID。所以不能隐藏。
                        // 但是我们不希望影响页面布局。
                        // 这里的 clone 只是为了提取文本，不会挂载到页面。所以样式无所谓。
                        el.prepend(mark);
                    });

                    let text = clone.innerText; 
                    // 如果 innerText 忽略了未挂载的元素的样式，它可能会显示所有文本。
                    // 这是一个兼容性问题。为了保险，我们使用 textContent 并自己处理格式？
                    // 或者将 clone 挂载到一个隐藏的 iframe?
                    // 简单起见，假设 clone.innerText 工作（在某些浏览器中如果不挂载，innerText 可能为空）。
                    if (!text || text.trim().length === 0) {
                         text = clone.textContent;
                    }

                    text = text.replace(/[\\r\\n]{3,}/g, '\\n\\n');
                    return text;
                } catch (e) {
                    return "Error reading page: " + e.message;
                }
            },

            scroll: function(deltaY) {
                window.scrollBy(0, deltaY);
                return { success: true };
            }
        };
        console.log("KunQiong AI Assistant Helper Injected");
    })();
    `;
    (document.head || document.documentElement).appendChild(script);
})();