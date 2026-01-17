namespace MiniWorldBrowser.Helpers
{
    public static class PageContentExtractor
    {
        public const string ExtractionScript = @"
(function() {
    // 简易版 Readability + Markdown 转换器

    function isHidden(node) {
        return node.offsetParent === null;
    }

    function getInnerText(node) {
        return node.innerText ? node.innerText.trim() : '';
    }

    // 1. 评分算法：找到主要内容区域
    function findMainContent() {
        // 如果有明确的 article 标签，且内容足够长，直接使用
        const articles = document.getElementsByTagName('article');
        for (let i = 0; i < articles.length; i++) {
            if (getInnerText(articles[i]).length > 200) {
                return articles[i];
            }
        }

        const candidates = [];
        const elements = document.querySelectorAll('p, div, section, td');

        for (let i = 0; i < elements.length; i++) {
            const node = elements[i];
            if (isHidden(node)) continue;

            const text = getInnerText(node);
            if (text.length < 50) continue;

            // 简单的评分
            let score = 0;
            score += text.length / 100;
            score += (text.match(/，|。|、/g) || []).length; // 中文标点
            score += (text.match(/, /g) || []).length; // 英文标点

            // 根据 class 和 id 加减分
            const className = (node.className || '').toLowerCase();
            const id = (node.id || '').toLowerCase();
            
            if (className.includes('article') || className.includes('content') || className.includes('main') || className.includes('post')) score += 10;
            if (id.includes('article') || id.includes('content') || id.includes('main') || id.includes('post')) score += 10;
            
            if (className.includes('nav') || className.includes('sidebar') || className.includes('footer') || className.includes('comment') || className.includes('menu')) score -= 20;
            if (id.includes('nav') || id.includes('sidebar') || id.includes('footer') || id.includes('comment') || id.includes('menu')) score -= 20;

            if (score > 5) {
                candidates.push({ node, score });
            }
        }

        // 排序找到最高分
        candidates.sort((a, b) => b.score - a.score);

        if (candidates.length > 0) {
            // 向上寻找父节点，直到包含足够多的高分节点
            let best = candidates[0].node;
            // 简单的向上回溯
            while (best.parentNode && best.parentNode !== document.body && best.parentNode.tagName !== 'HTML') {
                 // 如果父节点包含太多无关链接或噪音，停止
                 if ((best.parentNode.className || '').includes('body')) break;
                 best = best.parentNode;
                 // 限制回溯层级，防止选到 body
                 if (best.innerText.length > 5000 && best.tagName === 'DIV') break; 
            }
            return best;
        }

        return document.body;
    }

    // 2. HTML 转 Markdown
    function htmlToMarkdown(node) {
        let output = '';
        
        for (let child of node.childNodes) {
            if (child.nodeType === 3) { // 文本节点
                let text = child.textContent;
                // 压缩空白
                text = text.replace(/\s+/g, ' ');
                output += text;
            } else if (child.nodeType === 1) { // 元素节点
                if (isHidden(child)) continue;
                
                const tagName = child.tagName.toLowerCase();
                
                // 跳过无关元素
                if (['script', 'style', 'noscript', 'iframe', 'svg'].includes(tagName)) continue;
                
                let content = htmlToMarkdown(child);
                
                switch (tagName) {
                    case 'h1': output += '\n# ' + content + '\n\n'; break;
                    case 'h2': output += '\n## ' + content + '\n\n'; break;
                    case 'h3': output += '\n### ' + content + '\n\n'; break;
                    case 'h4': output += '\n#### ' + content + '\n\n'; break;
                    case 'h5': output += '\n##### ' + content + '\n\n'; break;
                    case 'h6': output += '\n###### ' + content + '\n\n'; break;
                    case 'p': output += '\n' + content + '\n\n'; break;
                    case 'br': output += '\n'; break;
                    case 'hr': output += '\n---\n\n'; break;
                    case 'blockquote': output += '\n> ' + content + '\n\n'; break;
                    case 'code': output += ' `' + content + '` '; break;
                    case 'pre': output += '\n```\n' + content + '\n```\n\n'; break;
                    case 'b':
                    case 'strong': output += ' **' + content + '** '; break;
                    case 'i':
                    case 'em': output += ' *' + content + '* '; break;
                    case 'a': 
                        const href = child.getAttribute('href');
                        if (href && content.trim().length > 0) output += '[' + content + '](' + href + ')';
                        else output += content;
                        break;
                    case 'img':
                        const src = child.getAttribute('src');
                        const alt = child.getAttribute('alt') || '';
                        if (src) output += '![' + alt + '](' + src + ')\n';
                        break;
                    case 'ul': output += '\n' + content + '\n'; break;
                    case 'ol': output += '\n' + content + '\n'; break;
                    case 'li': output += '- ' + content + '\n'; break;
                    case 'div': 
                    case 'section':
                    case 'article':
                    case 'main':
                        output += '\n' + content + '\n'; 
                        break;
                    default: output += content;
                }
            }
        }
        return output;
    }

    // 3. 提取交互元素
    function getInteractiveElements() {
        const interactive = [];
        // 扩大选择范围，包括链接和 select
        const elements = document.querySelectorAll('input, textarea, button, select, a, [role=""button""], [onclick]');
        
        let count = 0;
        for (let i = 0; i < elements.length; i++) {
            const el = elements[i];
            // 排除隐藏元素
            if (isHidden(el)) continue;
            
            // 分配唯一 ID
            const aiId = i + 1;
            el.setAttribute('data-ai-id', aiId);
            
            let label = '';
            if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                label = el.placeholder || el.name || el.value || el.getAttribute('aria-label') || 'Input';
                // 忽略隐藏的 input
                if (el.type === 'hidden') continue;
            } else if (el.tagName === 'SELECT') {
                label = el.getAttribute('aria-label') || el.name || 'Select';
            } else {
                label = el.innerText || el.title || el.getAttribute('aria-label') || '';
                // 如果是图片链接，尝试获取 img alt
                if (!label && el.querySelector('img')) {
                    label = el.querySelector('img').alt || 'Image';
                }
            }
            
            label = label.trim().replace(/\s+/g, ' ');
            
            // 过滤无意义的空链接
            if (label.length === 0) label = 'Unlabeled';
            if (el.tagName === 'A' && label === 'Unlabeled') continue;
            
            // 截断过长的标签
            if (label.length > 30) label = label.substring(0, 30) + '...';
            
            interactive.push(`${aiId}. [${el.tagName}] ${label}`);
            
            count++;
            if (count >= 100) break; // 限制数量防止 Token 溢出
        }
        
        return interactive.length > 0 ? '\n\n### Interactive Elements (Use ""click <id>"" or ""type <id> <text>"")\n' + interactive.join('\n') : '';
    }

    // 执行
    try {
        const mainNode = findMainContent();
        let markdown = htmlToMarkdown(mainNode);
        
        // 最后的清理：去除多余空行
        markdown = markdown.replace(/\n\s*\n\s*\n/g, '\n\n').trim();
        
        // 添加交互元素
        markdown += getInteractiveElements();
        
        // 添加标题和 URL
        const title = document.title;
        const url = window.location.href;
        
        return `Title: ${title}\nURL: ${url}\n\n${markdown}`;
    } catch (e) {
        return 'Error reading page: ' + e.message;
    }
})();
";
    }
}
