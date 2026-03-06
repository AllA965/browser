using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.Web.WebView2.Core;

namespace MiniWorldBrowser.Services
{
    /// <summary>
    /// 媒体资源下载服务，专门处理图片和视频的提取与下载
    /// </summary>
    public class MediaDownloadService
    {
        private readonly HttpClient _httpClient;

        public MediaDownloadService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// 从网页中提取所有图片并批量下载
        /// </summary>
        public async Task<int> ExtractAndDownloadImagesAsync(CoreWebView2 webView, string targetFolder)
        {
            var imageUrls = await ExtractImageUrlsAsync(webView);
            return await DownloadImagesAsync(imageUrls, targetFolder);
        }

        /// <summary>
        /// 仅从网页中提取所有图片 URL
        /// </summary>
        public async Task<List<string>> ExtractImageUrlsAsync(CoreWebView2 webView)
        {
            // 提取脚本：获取 img 标签和 CSS background-image
            const string script = @"(function() {
                const urls = new Set();
                
                // 1. 获取所有 img 标签
                document.querySelectorAll('img').forEach(img => {
                    if (img.src && img.src.startsWith('http')) urls.add(img.src);
                    if (img.dataset.src && img.dataset.src.startsWith('http')) urls.add(img.dataset.src);
                });

                // 2. 获取背景图片
                document.querySelectorAll('*').forEach(el => {
                    const style = window.getComputedStyle(el);
                    const bg = style.backgroundImage;
                    if (bg && bg !== 'none' && bg.includes('url(')) {
                        const match = bg.match(/url\(['""]?(.*?)['""]?\)/);
                        if (match && match[1] && match[1].startsWith('http')) {
                            urls.add(match[1]);
                        }
                    }
                });

                return JSON.stringify(Array.from(urls));
            })();";

            var result = await webView.ExecuteScriptAsync(script);
            var json = UnescapeJsString(result);
            
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }

        /// <summary>
        /// 尝试从当前页面提取实际的视频播放地址（通用提取 + 特定站点优化）
        /// </summary>
        public async Task<string> GetEffectiveVideoUrlAsync(MiniWorldBrowser.Browser.BrowserTab tab, string currentUrl)
        {
            var webView = tab.WebView.CoreWebView2;
            if (webView == null) return JsonSerializer.Serialize(new { type = "url", url = currentUrl });

            System.Diagnostics.Debug.WriteLine($"GetEffectiveVideoUrlAsync: {currentUrl}");

            // 获取当前页面的 Cookie 和 UA
            string cookieStr = "";
            string userAgent = "";
            try
            {
                var cookies = await webView.CookieManager.GetCookiesAsync(currentUrl);
                cookieStr = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                userAgent = webView.Settings.UserAgent;
            }
            catch { }

            // 1. 优先检查嗅探到的网络资源
            lock (tab.DetectedVideoResources)
            {
                if (tab.DetectedVideoResources.Count > 0)
                {
                    // 选取最新的一个视频资源
                    var latest = tab.DetectedVideoResources.Last();
                    return JsonSerializer.Serialize(new
                    {
                        type = "direct_data",
                        url = latest.Key,
                        title = webView.DocumentTitle ?? "嗅探到的视频",
                        cookies = cookieStr,
                        ua = userAgent
                    });
                }
            }

            // 2. 针对特定站点的深度提取 (如：抖音)
            if (currentUrl.Contains("douyin.com"))
            {
                var douyinResult = await ExtractDouyinVideoAsync(webView, currentUrl, cookieStr);
                if (!string.IsNullOrEmpty(douyinResult)) return douyinResult;
            }

            // 3. 通用 DOM 提取方案 (启发式搜索 <video> 标签和常见播放器结构)
            var genericResult = await ExtractGenericVideoAsync(webView);
            if (!string.IsNullOrEmpty(genericResult))
            {
                try
                {
                    // 注入 Cookie 和 UA
                    var doc = JsonDocument.Parse(genericResult);
                    if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "direct_data")
                    {
                        var resultObj = JsonSerializer.Deserialize<Dictionary<string, object>>(genericResult);
                        resultObj["cookies"] = cookieStr;
                        resultObj["ua"] = userAgent;
                        return JsonSerializer.Serialize(resultObj);
                    }
                }
                catch { }
                return genericResult;
            }

            // 4. 最后兜底：返回原始 URL，交给后端的 yt-dlp 处理
            return JsonSerializer.Serialize(new
            {
                type = "url",
                url = currentUrl,
                cookies = cookieStr,
                ua = userAgent
            });
        }

        private async Task<string> ExtractGenericVideoAsync(CoreWebView2 webView)
        {
            const string script = @"(function() {
                try {
                    // 寻找 <video> 标签
                    const videos = Array.from(document.querySelectorAll('video'));
                    for (const v of videos) {
                        const src = v.currentSrc || v.src;
                        if (src && src.startsWith('http')) {
                            return JSON.stringify({
                                type: 'direct_data',
                                url: src,
                                title: document.title || '网页视频',
                                duration: v.duration || 0
                            });
                        }
                        
                        // 寻找 <source> 子元素
                        const sources = Array.from(v.querySelectorAll('source'));
                        for (const s of sources) {
                            if (s.src && s.src.startsWith('http')) {
                                return JSON.stringify({
                                    type: 'direct_data',
                                    url: s.src,
                                    title: document.title || '网页视频'
                                });
                            }
                        }
                    }

                    // 寻找 iframe 嵌入 (常见于第三方播放器)
                    const iframes = Array.from(document.querySelectorAll('iframe'));
                    for (const f of iframes) {
                        const src = f.src;
                        if (src && (src.includes('player') || src.includes('video') || src.includes('m3u8'))) {
                             // 暂不深入提取 iframe 内部，但可以记录作为线索
                        }
                    }
                } catch (e) {}
                return null;
            })();";

            var result = await webView.ExecuteScriptAsync(script);
            return UnescapeJsString(result);
        }

        private async Task<string> ExtractDouyinVideoAsync(CoreWebView2 webView, string currentUrl, string cookieStr)
        {
            // 先尝试等待一小段时间，确保脚本已注入
            await Task.Delay(200);

            const string script = @"(async function() {
                try {
                    // --- A-Bogus Signature Algorithm (Scheme B) ---
                    const abLogic = (function() {
                        function rc4_encrypt(plaintext,key){var s=[];for(var i=0;i<256;i++){s[i]=i;}
                        var j=0;for(var i=0;i<256;i++){j=(j+s[i]+key.charCodeAt(i%key.length))%256;var temp=s[i];s[i]=s[j];s[j]=temp;}
                        var i=0;var j=0;var cipher=[];for(var k=0;k<plaintext.length;k++){i=(i+1)%256;j=(j+s[i])%256;var temp=s[i];s[i]=s[j];s[j]=temp;var t=(s[i]+s[j])%256;cipher.push(String.fromCharCode(s[t]^plaintext.charCodeAt(k)));}
                        return cipher.join('');}
                        function le(e,r){return(e<<(r%=32)|e>>>32-r)>>>0}
                        function de(e){return 0<=e&&e<16?2043430169:16<=e&&e<64?2055708042:void console['error'](""invalid j for constant Tj"")}
                        function pe(e,r,t,n){return 0<=e&&e<16?(r^t^n)>>>0:16<=e&&e<64?(r&t|r&n|t&n)>>>0:(console['error']('invalid j for bool function FF'),0)}
                        function he(e,r,t,n){return 0<=e&&e<16?(r^t^n)>>>0:16<=e&&e<64?(r&t|~r&n)>>>0:(console['error']('invalid j for bool function GG'),0)}
                        function SM3(){this.reg=[];this.chunk=[];this.size=0;this.reset()}
                        SM3.prototype.reset=function(){this.reg[0]=1937774191,this.reg[1]=1226093241,this.reg[2]=388252375,this.reg[3]=3666478592,this.reg[4]=2842636476,this.reg[5]=372324522,this.reg[6]=3817729613,this.reg[7]=2969243214,this[""chunk""]=[],this[""size""]=0};
                        SM3.prototype.write=function(e){var a=""string""==typeof e?function(e){var n=encodeURIComponent(e)['replace'](/%([0-9A-F]{2})/g,(function(e,r){return String['fromCharCode'](""0x""+r)})),a=new Array(n['length']);for(var i=0;i<=n.length-1;i++)
                        a[i]=n.charCodeAt(i);return a;}(e):e;this.size+=a.length;var f=64-this['chunk']['length'];if(a['length']<f)
                        this['chunk']=this['chunk'].concat(a);else
                        for(this['chunk']=this['chunk'].concat(a.slice(0,f));this['chunk'].length>=64;)
                        this['_compress'](this['chunk']),f<a['length']?this['chunk']=a['slice'](f,Math['min'](f+64,a['length'])):this['chunk']=[],f+=64};
                        SM3.prototype.sum=function(e,t){e&&(this['reset'](),this['write'](e)),this['_fill']();for(var f=0;f<this.chunk['length'];f+=64)
                        this._compress(this['chunk']['slice'](f,f+64));var i=null;if(t=='hex'){i="""";for(f=0;f<8;f++)
                        i+=this['reg'][f].toString(16).padStart(8,""0"")}else
                        for(i=new Array(32),f=0;f<8;f++){var c=this.reg[f];i[4*f+3]=(255&c)>>>0,c>>>=8,i[4*f+2]=(255&c)>>>0,c>>>=8,i[4*f+1]=(255&c)>>>0,c>>>=8,i[4*f]=(255&c)>>>0}
                        return this['reset'](),i};
                        SM3.prototype._compress=function(t){if(t<64)
                        console.error(""compress error: not enough data"");else{for(var f=function(e){for(var r=new Array(132),t=0;t<16;t++)
                        r[t]=e[4*t]<<24,r[t]|=e[4*t+1]<<16,r[t]|=e[4*t+2]<<8,r[t]|=e[4*t+3],r[t]>>>=0;for(var n=16;n<68;n++){var a=r[n-16]^r[n-9]^le(r[n-3],15);a=a^le(a,15)^le(a,23),r[n]=(a^le(r[n-13],7)^r[n-6])>>>0}
                        for(n=0;n<64;n++)
                        r[n+68]=(r[n]^r[n+4])>>>0;return r}
                        (t),i=this['reg'].slice(0),c=0;c<64;c++){var o=le(i[0],12)+i[4]+le(de(c),c),s=((o=le(o=(4294967295&o)>>>0,7))^le(i[0],12))>>>0,u=pe(c,i[0],i[1],i[2]);u=(4294967295&(u=u+i[3]+s+f[c+68]))>>>0;var b=he(c,i[4],i[5],i[6]);b=(4294967295&(b=b+i[7]+o+f[c]))>>>0,i[3]=i[2],i[2]=le(i[1],9),i[1]=i[0],i[0]=u,i[7]=i[6],i[6]=le(i[5],19),i[5]=i[4],i[4]=(b^le(b,9)^le(b,17))>>>0}
                        for(var l=0;l<8;l++)
                        this['reg'][l]=(this['reg'][l]^i[l])>>>0}};
                        SM3.prototype._fill=function(){var a=8*this['size'],f=this['chunk']['push'](128)%64;for(64-f<8&&(f-=64);f<56;f++)
                        this.chunk['push'](0);for(var i=0;i<4;i++){var c=Math['floor'](a/4294967296);this['chunk'].push(c>>>8*(3-i)&255)}
                        for(i=0;i<4;i++)
                        this['chunk']['push'](a>>>8*(3-i)&255)};
                        function result_encrypt(long_str,num){var s_obj={""s0"":""ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/="",""s1"":""Dkdpgh4ZKsQB80/Mfvw36XI1R25+WUAlEi7NLboqYTOPuzmFjJnryx9HVGcaStCe="",""s2"":""Dkdpgh4ZKsQB80/Mfvw36XI1R25-WUAlEi7NLboqYTOPuzmFjJnryx9HVGcaStCe="",""s3"":""ckdp1h4ZKsUB80/Mfvw36XIgR25+WQAlEi7NLboqYTOPuzmFjJnryx9HVGDaStCe"",""s4"":""Dkdpgh2ZmsQB80/MfvV36XI1R45-WUAlEixNLwoqYTOPuzKFjJnry79HbGcaStCe""}
                        var constant={""0"":16515072,""1"":258048,""2"":4032,""str"":s_obj[num]}
                        var result="""";var lound=0;var long_int=get_long_int(lound,long_str);for(var i=0;i<long_str.length/3*4;i++){if(Math.floor(i/4)!==lound){lound+=1;long_int=get_long_int(lound,long_str);}
                        var key=i%4;switch(key){case 0:temp_int=(long_int&constant[""0""])>>18;result+=constant[""str""].charAt(temp_int);break;case 1:temp_int=(long_int&constant[""1""])>>12;result+=constant[""str""].charAt(temp_int);break;case 2:temp_int=(long_int&constant[""2""])>>6;result+=constant[""str""].charAt(temp_int);break;case 3:temp_int=long_int&63;result+=constant[""str""].charAt(temp_int);break;default:break;}}
                        return result;}
                        function get_long_int(round,long_str){round=round*3;return(long_str.charCodeAt(round)<<16)|(long_str.charCodeAt(round+1)<<8)|(long_str.charCodeAt(round+2));}
                        function gener_random(random,option){return[(random&255&170)|option[0]&85,(random&255&85)|option[0]&170,(random>>8&255&170)|option[1]&85,(random>>8&255&85)|option[1]&170,]}
                        function generate_rc4_bb_str(url_search_params,user_agent,window_env_str,suffix,Arguments){suffix=suffix||'cus';Arguments=Arguments||[0,1,14];var sm3=new SM3();var start_time=Date.now();var url_search_params_list=sm3.sum(sm3.sum(url_search_params+suffix));var cus=sm3.sum(sm3.sum(suffix));var ua=sm3.sum(result_encrypt(rc4_encrypt(user_agent,String.fromCharCode.apply(null,[0.00390625,1,14])),""s3""));var end_time=Date.now();var b={8:3,10:end_time,15:{""aid"":6383,""pageId"":6241,""boe"":false,""ddrt"":7,""paths"":{""include"":[{},{},{},{},{},{},{}],""exclude"":[]},""track"":{""mode"":0,""delay"":300,""paths"":[]},""dump"":true,""rpU"":""""},16:start_time,18:44,19:[1,0,1,5]}
                        b[20]=(b[16]>>24)&255
                        b[21]=(b[16]>>16)&255
                        b[22]=(b[16]>>8)&255
                        b[23]=b[16]&255
                        b[24]=(b[16]/256/256/256/256)>>0
                        b[25]=(b[16]/256/256/256/256/256)>>0
                        b[26]=(Arguments[0]>>24)&255
                        b[27]=(Arguments[0]>>16)&255
                        b[28]=(Arguments[0]>>8)&255
                        b[29]=Arguments[0]&255
                        b[30]=(Arguments[1]/256)&255
                        b[31]=(Arguments[1]%256)&255
                        b[32]=(Arguments[1]>>24)&255
                        b[33]=(Arguments[1]>>16)&255
                        b[34]=(Arguments[2]>>24)&255
                        b[35]=(Arguments[2]>>16)&255
                        b[36]=(Arguments[2]>>8)&255
                        b[37]=Arguments[2]&255
                        b[38]=url_search_params_list[21]
                        b[39]=url_search_params_list[22]
                        b[40]=cus[21]
                        b[41]=cus[22]
                        b[42]=ua[23]
                        b[43]=ua[24]
                        b[44]=(b[10]>>24)&255
                        b[45]=(b[10]>>16)&255
                        b[46]=(b[10]>>8)&255
                        b[47]=b[10]&255
                        b[48]=b[8]
                        b[49]=(b[10]/256/256/256/256)>>0
                        b[50]=(b[10]/256/256/256/256/256)>>0
                        b[51]=b[15]['pageId']
                        b[52]=(b[15]['pageId']>>24)&255
                        b[53]=(b[15]['pageId']>>16)&255
                        b[54]=(b[15]['pageId']>>8)&255
                        b[55]=b[15]['pageId']&255
                        b[56]=b[15]['aid']
                        b[57]=b[15]['aid']&255
                        b[58]=(b[15]['aid']>>8)&255
                        b[59]=(b[15]['aid']>>16)&255
                        b[60]=(b[15]['aid']>>24)&255
                        var window_env_list=[];for(var index=0;index<window_env_str.length;index++){window_env_list.push(window_env_str.charCodeAt(index))}
                        b[64]=window_env_list.length
                        b[65]=b[64]&255
                        b[66]=(b[64]>>8)&255
                        b[69]=[].length
                        b[70]=b[69]&255
                        b[71]=(b[69]>>8)&255
                        b[72]=b[18]^b[20]^b[26]^b[30]^b[38]^b[40]^b[42]^b[21]^b[27]^b[31]^b[35]^b[39]^b[41]^b[43]^b[22]^b[28]^b[32]^b[36]^b[23]^b[29]^b[33]^b[37]^b[44]^b[45]^b[46]^b[47]^b[48]^b[49]^b[50]^b[24]^b[25]^b[52]^b[53]^b[54]^b[55]^b[57]^b[58]^b[59]^b[60]^b[65]^b[66]^b[70]^b[71]
                        var bb=[b[18],b[20],b[52],b[26],b[30],b[34],b[58],b[38],b[40],b[53],b[42],b[21],b[27],b[54],b[55],b[31],b[35],b[57],b[39],b[41],b[43],b[22],b[28],b[32],b[60],b[36],b[23],b[29],b[33],b[37],b[44],b[45],b[59],b[46],b[47],b[48],b[49],b[50],b[24],b[25],b[65],b[66],b[70],b[71]]
                        bb=bb.concat(window_env_list).concat(b[72]);return rc4_encrypt(String.fromCharCode.apply(null,bb),String.fromCharCode.apply(null,[121]));}
                        function generate_random_str(){var random_str_list=[];random_str_list=random_str_list.concat(gener_random(Math.random()*10000,[3,45]))
                        random_str_list=random_str_list.concat(gener_random(Math.random()*10000,[1,0]))
                        random_str_list=random_str_list.concat(gener_random(Math.random()*10000,[1,5]))
                        return String.fromCharCode.apply(null,random_str_list);}
                        function a_bogus(url_search_params,user_agent){user_agent=user_agent||navigator.userAgent;var result_str=generate_random_str()+generate_rc4_bb_str(url_search_params,user_agent,""1536|747|1536|834|0|30|0|0|1536|834|1536|864|1525|747|24|24|Win32"");return encodeURIComponent(result_encrypt(result_str,""s4"")+""="");}
                        return a_bogus;
                    })();

                    const getRawData = () => {
                        const el = document.getElementById('RENDER_DATA') || 
                                   document.querySelector('script[type=""application/json""]#RENDER_DATA');
                        if (el && el.innerText) return JSON.parse(decodeURIComponent(el.innerText));
                        
                        const scripts = Array.from(document.querySelectorAll('script'));
                        for (const s of scripts) {
                            const content = s.innerText;
                            if (content.includes('aweme_id') && content.includes('play_addr')) {
                                const match = content.match(/window\.(?:_SSR_DATA|__INITIAL_STATE__|__ROUTER_DATA__|_ROUTER_DATA)\s*=\s*({.*?});/);
                                if (match) return JSON.parse(match[1]);
                                const jsonMatch = content.match(/({""app"":{.*""videoDetail"":{.*}})/) || content.match(/({""aweme"":{.*""detail"":{.*}})/);
                                if (jsonMatch) return JSON.parse(jsonMatch[1]);
                            }
                        }
                        
                        const globals = ['_SSR_DATA', '__INITIAL_STATE__', '__ROUTER_DATA__', '_ROUTER_DATA'];
                        for (const g of globals) {
                            if (window[g]) return window[g];
                        }
                        return null;
                    };

                    const findVideoById = (root, awemeId) => {
                        const visit = (o) => {
                            if (!o || typeof o !== 'object') return null;
                            if (o.awemeId === awemeId || o.aweme_id === awemeId || (o.aweme && (o.aweme.awemeId === awemeId || o.aweme.aweme_id === awemeId))) {
                                return o.aweme || o;
                            }
                            for (let k in o) {
                                if (Object.prototype.hasOwnProperty.call(o, k) && typeof o[k] === 'object') {
                                    const res = visit(o[k]);
                                    if (res) return res;
                                }
                            }
                            return null;
                        };
                        return visit(root);
                    };

                    const findFirstVideoObject = (root) => {
                        const visit = (o) => {
                            if (!o || typeof o !== 'object') return null;
                            if (o.video && (o.video.play_addr || o.video.playAddr || o.video.play_addr_h265)) {
                                return o;
                            }
                            for (let k in o) {
                                if (Object.prototype.hasOwnProperty.call(o, k) && typeof o[k] === 'object') {
                                    const res = visit(o[k]);
                                    if (res) return res;
                                }
                            }
                            return null;
                        };
                        return visit(root);
                    };

                    const getActiveId = () => {
                        const selectors = [
                            '[data-e2e=""feed-active-video""]',
                            '.swiper-slide-active',
                            '.active-video-container',
                            '[data-e2e=""user-post-item""]'
                        ];
                        
                        for (const selector of selectors) {
                            const activeEl = document.querySelector(selector);
                            if (activeEl) {
                                const link = activeEl.querySelector('a[href*=""/video/""]');
                                if (link) {
                                    const m = link.getAttribute('href').match(/\/video\/(\d+)/);
                                    if (m) return m[1];
                                }
                            }
                        }
                        
                        const videos = Array.from(document.querySelectorAll('video'));
                        let bestVideo = null;
                        let maxScore = -1;
                        
                        for (const v of videos) {
                            const rect = v.getBoundingClientRect();
                            const isVisible = rect.top < window.innerHeight && rect.bottom > 0;
                            if (!isVisible) continue;
                            
                            let score = 0;
                            if (!v.paused) score += 100;
                            if (v.readyState > 2) score += 50;
                            
                            const centerY = window.innerHeight / 2;
                            const videoCenterY = rect.top + rect.height / 2;
                            score += (1 - Math.abs(videoCenterY - centerY) / window.innerHeight) * 50;
                            
                            if (score > maxScore) {
                                maxScore = score;
                                bestVideo = v;
                            }
                        }
                        
                        if (bestVideo) {
                            let p = bestVideo.parentElement;
                            for (let i=0; i<25 && p; i++) {
                                const link = p.querySelector('a[href*=""/video/""]');
                                if (link) {
                                    const m = link.getAttribute('href').match(/\/video\/(\d+)/);
                                    if (m) return m[1];
                                }
                                p = p.parentElement;
                            }
                        }
                        
                        const pathMatch = window.location.pathname.match(/\/(?:video|note)\/([^/?]+)/);
                        if (pathMatch) return pathMatch[1];
                        
                        const searchMatch = window.location.search.match(/modal_id=([^&]+)/);
                        if (searchMatch) return searchMatch[1];
                        
                        const allVideoLinks = Array.from(document.querySelectorAll('a[href*=""/video/""]'));
                        for (const link of allVideoLinks) {
                            const r = link.getBoundingClientRect();
                            if (r.width > 0 && r.height > 0) {
                                const m = link.getAttribute('href').match(/\/video\/(\d+)/);
                                if (m) return m[1];
                            }
                        }
                        
                        return null;
                    };

                    const formatVData = (vData) => {
                        if (!vData || !vData.video) return null;
                        const addr = vData.video.play_addr_h265 || vData.video.playAddr || vData.video.play_addr;
                        const urls = addr && (addr.urlList || addr.url_list);
                        if (urls && urls.length > 0) {
                            return JSON.stringify({
                                type: 'direct_data',
                                title: vData.desc || (vData.desc_info && vData.desc_info.desc) || '抖音视频',
                                url: urls[0].replace('playwm', 'play'),
                                cover: (vData.video.cover && (vData.video.cover.urlList || vData.video.cover.url_list) || [])[0],
                                duration: (vData.video.duration || 0) / 1000,
                                cookies: 'COOKIE_PLACEHOLDER',
                                ua: navigator.userAgent
                            });
                        }
                        return null;
                    };

                    const jsonData = getRawData();
                    const activeId = getActiveId();

                    if (activeId) {
                        try {
                            const aid = 6383;
                            const params = `device_platform=webapp&aid=${aid}&channel=channel_pc_web&aweme_id=${activeId}&update_version_code=170400&pc_client_type=1&version_code=190500&version_name=19.5.0&cookie_enabled=true&screen_width=${window.screen.width}&screen_height=${window.screen.height}&browser_language=zh-CN&browser_platform=Win32&browser_name=Chrome&browser_version=120.0.0.0&browser_online=true&engine_name=Blink&engine_version=120.0.0.0&os_name=Windows&os_version=10&cpu_core_num=16&device_memory=8&platform=PC&webid=7300000000000000000`;
                            const bogus = abLogic(params, navigator.userAgent);
                            const apiUrl = `https://www.douyin.com/aweme/v1/web/aweme/detail/?${params}&a_bogus=${bogus}`;
                            
                            const fetchRes = await fetch(apiUrl, {
                                headers: {
                                    'Referer': 'https://www.douyin.com/',
                                    'User-Agent': navigator.userAgent
                                }
                            });
                            const apiData = await fetchRes.json();
                            if (apiData && apiData.aweme_detail) {
                                const result = formatVData(apiData.aweme_detail);
                                if (result) return result;
                            }
                        } catch (apiErr) {
                            console.log('Scheme B API call failed:', apiErr);
                        }
                    }

                    let vData = null;
                    if (activeId && jsonData) {
                        vData = findVideoById(jsonData, activeId);
                    }
                    if (!vData && jsonData) {
                        vData = findFirstVideoObject(jsonData);
                    }
                    const schemeAResult = formatVData(vData);
                    if (schemeAResult) return schemeAResult;
                    
                    const finalUrl = activeId ? 'https://www.douyin.com/video/' + activeId : window.location.href;
                    return JSON.stringify({ 
                        type: 'url', 
                        url: finalUrl,
                        cookies: 'COOKIE_PLACEHOLDER',
                        ua: navigator.userAgent
                    });
                } catch (e) { 
                    return JSON.stringify({ 
                        type: 'url', 
                        url: window.location.href, 
                        cookies: 'COOKIE_PLACEHOLDER',
                        ua: navigator.userAgent,
                        debug: e.message 
                    });
                }
            })();";

            var result = await webView.ExecuteScriptAsync(script);
            var unescaped = UnescapeJsString(result);
            
            if (!string.IsNullOrEmpty(cookieStr)) {
                unescaped = unescaped.Replace("COOKIE_PLACEHOLDER", cookieStr.Replace("\"", "\\\""));
            }

            System.Diagnostics.Debug.WriteLine($"Extraction Result: {unescaped}");
            return unescaped;
        }

        /// <summary>
        /// 批量下载图片到目标文件夹
        /// </summary>
        public async Task<int> DownloadImagesAsync(List<string> imageUrls, string targetFolder)
        {
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            int count = 0;
            var tasks = imageUrls.Select(async url =>
            {
                try
                {
                    var uri = new Uri(url);
                    var fileName = Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrEmpty(fileName) || !Path.HasExtension(fileName))
                    {
                        fileName = Guid.NewGuid().ToString("N")[..8] + ".jpg";
                    }

                    var filePath = Path.Combine(targetFolder, fileName);
                    
                    var data = await _httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(filePath, data);
                    count++;
                }
                catch
                {
                    // 忽略单个下载失败
                }
            });

            await Task.WhenAll(tasks);
            return count;
        }

        /// <summary>
        /// 反转义 WebView2 返回的字符串
        /// </summary>
        private static string UnescapeJsString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            if (str.StartsWith("\"") && str.EndsWith("\""))
            {
                str = str[1..^1];
                str = System.Text.RegularExpressions.Regex.Unescape(str);
            }
            return str;
        }
        /// <summary>
        /// 获取外部下载任务的进度
        /// </summary>
        public async Task<DownloadProgressInfo?> GetDownloadProgressAsync(string taskId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://localhost:8000/video/progress/{taskId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<DownloadProgressInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }

        public class DownloadProgressInfo
        {
            public string Status { get; set; } = "";
            public double Progress { get; set; }
            public string Filename { get; set; } = "";
            [JsonPropertyName("total_bytes")]
            public long TotalBytes { get; set; }
            [JsonPropertyName("downloaded_bytes")]
            public long DownloadedBytes { get; set; }
            public double Speed { get; set; }
            public double Eta { get; set; }
            public string Error { get; set; } = "";
        }
    }
}
