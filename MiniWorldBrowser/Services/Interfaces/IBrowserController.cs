namespace MiniWorldBrowser.Services.Interfaces
{
    public interface IBrowserController
    {
        void Navigate(string url);
        void GoBack();
        void GoForward();
        void Refresh();
        void Search(string query);
        void SearchOnSite(string query, string siteName);
        void NewTab(string url);
        void CloseCurrentTab();
        void Scroll(int deltaY);
        string GetCurrentUrl();
        Task<string> GetPageContentAsync();
        void ClickElement(string selector);
        void ClickElementById(string id);
        void TypeToElement(string selector, string text);
        void TypeToElementById(string id, string text);
    }
}
