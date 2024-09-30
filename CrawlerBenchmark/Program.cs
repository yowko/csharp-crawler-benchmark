using System.Text.Json;
using System.Web;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.XPath;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Playwright;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using PuppeteerSharp;
using PuppeteerSharp.Helpers;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using IPage = PuppeteerSharp.IPage;

BenchmarkRunner.Run<Crawler>();

[SimpleJob(RunStrategy.Monitoring, launchCount: 2, warmupCount: 2, iterationCount: 20)]
[MemoryDiagnoser(false)]
public class Crawler
{
    const string TargetUrl = "https://tw.stock.yahoo.com/class-quote?sectorId=26&exchange=TAI";
    private HtmlDocument htmlAgilityPackHtml = default;
    private HtmlWeb htmlAgilityPackHtmlWeb = default;

    private HtmlNode scrapySharpHtml = default;
    private ScrapingBrowser scrapySharpbrowser = default;

    private IDocument angleSharpDocument = default;
    private IPage puppeteerSharpPage = default;
    private ChromeDriver seleniumDriver = default;
    private Microsoft.Playwright.IPage playwrightPage = default;
    private string htmlDocument = string.Empty;
    private const bool ShowDebug = false;


    private const bool UseHtmlString = false;
    //[Params(true, false)] public bool UseHtmlString { get; set; }


    [GlobalSetup]
    public async Task Setup()
    {
        //下載瀏覽器，若已下載過則不會再次下載
        await new BrowserFetcher().DownloadAsync();
        if (UseHtmlString)
        {
            HttpClient client = new HttpClient();
            htmlDocument = await client.GetStringAsync(TargetUrl);
        }

        htmlAgilityPackHtml = new HtmlDocument();
        htmlAgilityPackHtmlWeb = new HtmlWeb();
        
        // 建立瀏覽器物件
        scrapySharpbrowser = new ScrapingBrowser();
        
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Timeout = 0,
            Args = new[] { "--disable-features=site-per-process", "--no-sandbox" }
        });
        TaskHelper.DefaultTimeout = 5_000;
        puppeteerSharpPage = await browser.NewPageAsync();

        
        var options = new ChromeOptions();
        //使用 headless 模式
        options.AddArguments(new List<string>()
        {
            "headless",
            "disable-gpu",
            "--remote-debugging-pipe",
            "no-sandbox"
        });
        options.PageLoadTimeout = TimeSpan.MaxValue;
        //使用 ChromeDriver
        seleniumDriver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), options, TimeSpan.FromMinutes(3));
       
        // Install Playwright
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
        if (exitCode != 0)
        {
            throw new Exception($"Playwright exited with code {exitCode}");
        }
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var playwrightBrowser = await playwright.Chromium.LaunchAsync();
        playwrightPage = await playwrightBrowser.NewPageAsync();
        
    }

    [Benchmark]
    public Task HtmlAgilityPackCssSelectorsNetCore()
    {
        var html = htmlAgilityPackHtml;

        if (UseHtmlString)
        {
            html.LoadHtml(htmlDocument);
        }
        else
        {
            html = htmlAgilityPackHtmlWeb.Load(TargetUrl);
        }

        var nodes = Enumerable.Empty<HtmlNode>();
        nodes = HtmlAgilityPackCssSelectorsNetCore(html);

        //將 html node 轉換成 Stock 物件
        var stocks = nodes!.Select(a => new Stock
        {
            //以下使用 xpath 取得股票資訊
            Name = HttpUtility.HtmlDecode(a.SelectSingleNode("./div/div[1]/div[2]/div/div[1]").InnerText
                .Trim()), //將 html entity 轉換成字串
            Symbol = a.SelectSingleNode("./div/div[1]/div[2]/div/div[2]").InnerText.Trim(),
            Price = a.SelectSingleNode("./div/div[2]").InnerText.Trim(),
            PriceChange = a.SelectSingleNode("./div/div[3]").InnerText.Trim(),
            Change = a.SelectSingleNode("./div/div[4]").InnerText.Trim(),
            Open = a.SelectSingleNode("./div/div[5]").InnerText.Trim(),
            LastClose = a.SelectSingleNode("./div/div[6]").InnerText.Trim(),
            High = a.SelectSingleNode("./div/div[7]").InnerText.Trim(),
            Low = a.SelectSingleNode("./div/div[8]").InnerText.Trim(),
            Turnover = a.SelectSingleNode("./div/div[9]").InnerText.Trim(),
            UpDown = a.SelectSingleNode("./div/div[3]/span").Attributes["class"].Value
            //UpDownCheck(a.SelectSingleNode("./div/div[3]/span").Attributes["class"].Value) //處理上漲或下跌的顯示
        });
        if (ShowDebug)
        {
            foreach (var stock in stocks)
            {
                Console.WriteLine(
                    $"股票名稱: {stock.Name,-12}\t 股票代號: {stock.Symbol}\t 股價: {stock.Price,-5}\t 漲跌: {stock.UpDown} {stock.PriceChange,-8}\t 漲跌幅: {stock.UpDown} {stock.Change,-8}\t 開盤: {stock.Open}\t 昨收: {stock.LastClose}\t 最高: {stock.High}\t 最低: {stock.Low}\t 成交量(張): {stock.Turnover}");
            }
        }

        return Task.CompletedTask;

        IEnumerable<HtmlNode> HtmlAgilityPackCssSelectorsNetCore(HtmlDocument doc)
        {
            //使用 css selector 取得所有 etf 股票的 html node
            //return HtmlAgilityPack.CssSelectors.NetCore.HapCssExtensionMethods.QuerySelectorAll(doc, "li.List(n)");
            return doc.QuerySelectorAll("li.List(n)");
            //return htmlDocument.QuerySelectorAll("li.List(n)");
        }
    }

    [Benchmark]
    public Task FizzlerSystemsHtmlAgilityPack()
    {
        var html = htmlAgilityPackHtml;
        if (UseHtmlString)
        {
            html.LoadHtml(htmlDocument);
        }
        else
        {
            html = htmlAgilityPackHtmlWeb.Load(TargetUrl);
        }

        var nodes = Enumerable.Empty<HtmlNode>();
        nodes = FizzlerSystemsHtmlAgilityPack(html);

        //將 html node 轉換成 Stock 物件
        var stocks = nodes!.Select(a => new Stock
        {
            //以下使用 xpath 取得股票資訊
            Name = HttpUtility.HtmlDecode(a.SelectSingleNode("./div/div[1]/div[2]/div/div[1]").InnerText
                .Trim()), //將 html entity 轉換成字串
            Symbol = a.SelectSingleNode("./div/div[1]/div[2]/div/div[2]").InnerText.Trim(),
            Price = a.SelectSingleNode("./div/div[2]").InnerText.Trim(),
            PriceChange = a.SelectSingleNode("./div/div[3]").InnerText.Trim(),
            Change = a.SelectSingleNode("./div/div[4]").InnerText.Trim(),
            Open = a.SelectSingleNode("./div/div[5]").InnerText.Trim(),
            LastClose = a.SelectSingleNode("./div/div[6]").InnerText.Trim(),
            High = a.SelectSingleNode("./div/div[7]").InnerText.Trim(),
            Low = a.SelectSingleNode("./div/div[8]").InnerText.Trim(),
            Turnover = a.SelectSingleNode("./div/div[9]").InnerText.Trim(),
            UpDown = a.SelectSingleNode("./div/div[3]/span").Attributes["class"].Value
            //UpDownCheck(a.SelectSingleNode("./div/div[3]/span").Attributes["class"].Value) //處理上漲或下跌的顯示
        });

        if (ShowDebug)
        {
            foreach (var stock in stocks)
            {
                Console.WriteLine(
                    $"股票名稱: {stock.Name,-12}\t 股票代號: {stock.Symbol}\t 股價: {stock.Price,-5}\t 漲跌: {stock.UpDown} {stock.PriceChange,-8}\t 漲跌幅: {stock.UpDown} {stock.Change,-8}\t 開盤: {stock.Open}\t 昨收: {stock.LastClose}\t 最高: {stock.High}\t 最低: {stock.Low}\t 成交量(張): {stock.Turnover}");
            }
        }

        //Task.CompletedTask.Wait();
        return Task.CompletedTask;

        IEnumerable<HtmlNode> FizzlerSystemsHtmlAgilityPack(HtmlDocument doc)
        {
            //使用 css selector 取得所有 etf 股票的 html node
            return Fizzler.Systems.HtmlAgilityPack.HtmlNodeSelection.QuerySelectorAll(doc.DocumentNode,
                "li[class='List(n)']");
            //return Fizzler.Systems.HtmlAgilityPack.HtmlNodeSelection.QuerySelectorAll(htmlDocument.DocumentNode,"li[class='List(n)']");
        }
    }

    [Benchmark]
    public Task ScrapySharp()
    {
        HtmlNode html = scrapySharpHtml;

        if (UseHtmlString)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlDocument);
            html = doc.DocumentNode;
        }
        else
        {
            html = scrapySharpbrowser.NavigateToPage(new Uri(TargetUrl)).Html;
        }

        //

        //使用 css selector 取得所有 etf 股票的 html node
        var nodes = html.CssSelect("li[class='List(n)']");

        //將 html node 轉換成 Stock 物件
        var stocks = nodes.Select(a => new Stock
        {
            //以下使用 xpath 取得股票資訊
            Name = HttpUtility.HtmlDecode(a.SelectSingleNode("./div/div[1]/div[2]/div/div[1]").InnerText
                .Trim()), //將 html entity 轉換成字串
            Symbol = a.SelectSingleNode("./div/div[1]/div[2]/div/div[2]").InnerText.Trim(),
            Price = a.SelectSingleNode("./div/div[2]").InnerText.Trim(),
            PriceChange = a.SelectSingleNode("./div/div[3]").InnerText.Trim(),
            Change = a.SelectSingleNode("./div/div[4]").InnerText.Trim(),
            Open = a.SelectSingleNode("./div/div[5]").InnerText.Trim(),
            LastClose = a.SelectSingleNode("./div/div[6]").InnerText.Trim(),
            High = a.SelectSingleNode("./div/div[7]").InnerText.Trim(),
            Low = a.SelectSingleNode("./div/div[8]").InnerText.Trim(),
            Turnover = a.SelectSingleNode("./div/div[9]").InnerText.Trim(),
            UpDown = a.SelectSingleNode("./div/div[3]/span").Attributes["class"].Value
            //UpDownCheck(a.SelectSingleNode("./div/div[3]/span").Attributes["class"].Value) //處理上漲或下跌的顯示
        });

        if (ShowDebug)
        {
            foreach (var stock in stocks)
            {
                Console.WriteLine(
                    $"股票名稱: {stock.Name,-12}\t 股票代號: {stock.Symbol}\t 股價: {stock.Price,-5}\t 漲跌: {stock.UpDown} {stock.PriceChange,-8}\t 漲跌幅: {stock.UpDown} {stock.Change,-8}\t 開盤: {stock.Open}\t 昨收: {stock.LastClose}\t 最高: {stock.High}\t 最低: {stock.Low}\t 成交量(張): {stock.Turnover}");
            }
        }

        return Task.CompletedTask;
    }

    [Benchmark]
    public async Task AngleSharp()
    {
        IDocument document = angleSharpDocument;

        var config = Configuration.Default.WithDefaultLoader();
        if (UseHtmlString)
        {
            document = await BrowsingContext.New(config).OpenAsync(req => req.Content(htmlDocument));
        }
        else
        {
            document = await BrowsingContext.New(config).OpenAsync(TargetUrl);
        }

        //使用 css selector 取得所有 etf 股票的 html node
        var nodes = //document.QuerySelectorAll("li[class='List(n)']");
            document.All.Where(a => a is { LocalName: "li", ClassName: "List(n)" });
        //document.QuerySelectorAll("li.List(n)");//這會造成錯誤

        //將 html node 轉換成 Stock 物件
        var stocks = nodes.Select(a => new Stock
        {
            //以下使用 xpath 取得股票資訊
            Name = HttpUtility.HtmlDecode(a.SelectSingleNode("./div/div[1]/div[2]/div/div[1]").TextContent
                .Trim()), //將 html entity 轉換成字串
            Symbol = a.SelectSingleNode("./div/div[1]/div[2]/div/div[2]").TextContent.Trim(),
            Price = a.SelectSingleNode("./div/div[2]").TextContent.Trim(),
            PriceChange = a.SelectSingleNode("./div/div[3]").TextContent.Trim(),
            Change = a.SelectSingleNode("./div/div[4]").TextContent.Trim(),
            Open = a.SelectSingleNode("./div/div[5]").TextContent.Trim(),
            LastClose = a.SelectSingleNode("./div/div[6]").TextContent.Trim(),
            High = a.SelectSingleNode("./div/div[7]").TextContent.Trim(),
            Low = a.SelectSingleNode("./div/div[8]").TextContent.Trim(),
            Turnover = a.SelectSingleNode("./div/div[9]").TextContent.Trim(),
            UpDown = ((IElement)a.SelectSingleNode("./div/div[3]/span")).ClassName!
            //UpDownCheck(((IElement)a.SelectSingleNode("./div/div[3]/span")).ClassName)
            //UpDownCheck(((IElement)a.SelectSingleNode("./div/div[3]/span")).GetAttribute("class")) //處理上漲或下跌的顯示
        });

        if (ShowDebug)
        {
            foreach (var stock in stocks)
            {
                Console.WriteLine(
                    $"股票名稱: {stock.Name,-12}\t 股票代號: {stock.Symbol}\t 股價: {stock.Price,-5}\t 漲跌: {stock.UpDown} {stock.PriceChange,-8}\t 漲跌幅: {stock.UpDown} {stock.Change,-8}\t 開盤: {stock.Open}\t 昨收: {stock.LastClose}\t 最高: {stock.High}\t 最低: {stock.Low}\t 成交量(張): {stock.Turnover}");
            }
        }
    }

    [Benchmark]
    public async Task PuppeteerSharp()
    {
        var page = puppeteerSharpPage;

        if (UseHtmlString)
        {
            await page.SetContentAsync(htmlDocument, new NavigationOptions() { Timeout = 300000 });
        }
        else
        {
            await page.GoToAsync(TargetUrl, new NavigationOptions() { Timeout = 0 });
        }

        //使用 css 選擇器取得所有股票眕內容，再透過 XPath 取得股票詳細資訊
        var jsSelectAllStocks = @"Array.from(document.querySelectorAll('li[class=""List(n)""]')).map(a => {
            return {
                Name: document.evaluate('./div/div[1]/div[2]/div/div[1]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                Symbol: document.evaluate('./div/div[1]/div[2]/div/div[2]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                Price: document.evaluate('./div/div[2]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                PriceChange: document.evaluate('./div/div[3]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                Change: document.evaluate('./div/div[4]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                Open: document.evaluate('./div/div[5]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                LastClose: document.evaluate('./div/div[6]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                High: document.evaluate('./div/div[7]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                Low: document.evaluate('./div/div[8]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                Turnover: document.evaluate('./div/div[9]', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.innerText,
                UpDown: document.evaluate('./div/div[3]/span', a, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.className
            };
        });";

        var matchHtml = await page.EvaluateExpressionAsync<JsonDocument[]>(jsSelectAllStocks);
        var stocks = matchHtml.Select(a => a.Deserialize<Stock>());

        if (ShowDebug)
        {
            foreach (var stock in stocks)
            {
                Console.WriteLine(
                    $"股票名稱: {stock.Name,-12}\t 股票代號: {stock.Symbol}\t 股價: {stock.Price,-5}\t 漲跌: {stock.UpDown} {stock.PriceChange,-8}\t 漲跌幅: {stock.UpDown} {stock.Change,-8}\t 開盤: {stock.Open}\t 昨收: {stock.LastClose}\t 最高: {stock.High}\t 最低: {stock.Low}\t 成交量(張): {stock.Turnover}");
            }
        }
    }

    [Benchmark]
    public Task Selenium()
    {
        var browser = seleniumDriver;
        if (UseHtmlString)
        {
            // 將 HTML 字串寫入臨時檔案
            string tempFilePath = Path.Combine(Path.GetTempPath(), "tempPage.html");
            File.WriteAllText(tempFilePath, htmlDocument);
            browser.Navigate().GoToUrl("file:///" + tempFilePath);
        }
        else
        {
            browser.Navigate().GoToUrl(TargetUrl);
        }

        //使用 css selector 找到所有股票資訊
        var nodes = browser.FindElements(By.CssSelector("li[class='List(n)']"));

        var stocks = nodes.Select(a => new Stock
        {
            //使用 XPath 找到股票名稱、代號、價格、漲跌、漲跌幅、開盤、昨收、最高、最低、成交量
            Name = a.FindElement(By.XPath("./div/div[1]/div[2]/div/div[1]")).Text.Trim(),
            Symbol = a.FindElement(By.XPath("./div/div[1]/div[2]/div/div[2]")).Text.Trim(),
            Price = a.FindElement(By.XPath("./div/div[2]")).Text.Trim(),
            Change = a.FindElement(By.XPath("./div/div[3]")).Text.Trim(),
            PriceChange = a.FindElement(By.XPath("./div/div[4]")).Text.Trim(),
            Open = a.FindElement(By.XPath("./div/div[5]")).Text.Trim(),
            LastClose = a.FindElement(By.XPath("./div/div[6]")).Text.Trim(),
            High = a.FindElement(By.XPath("./div/div[7]")).Text.Trim(),
            Low = a.FindElement(By.XPath("./div/div[8]")).Text.Trim(),
            Turnover = a.FindElement(By.XPath("./div/div[9]")).Text.Trim(),
            //UpDown = UpDownCheck(a.FindElement(By.XPath("./div/div[3]/span")).GetAttribute("class"))
            UpDown = a.FindElement(By.XPath("./div/div[3]/span")).GetAttribute("class")
        });
        if (ShowDebug)
        {
            foreach (var stock in stocks)
            {
                Console.WriteLine(
                    $"股票名稱: {stock.Name,-12}\t 股票代號: {stock.Symbol}\t 股價: {stock.Price,-5}\t 漲跌: {stock.UpDown} {stock.PriceChange,-8}\t 漲跌幅: {stock.UpDown} {stock.Change,-8}\t 開盤: {stock.Open}\t 昨收: {stock.LastClose}\t 最高: {stock.High}\t 最低: {stock.Low}\t 成交量(張): {stock.Turnover}");
            }
        }

        return Task.CompletedTask;
    }

    [Benchmark]
    public async Task Playwright()
    {
        var page = playwrightPage;
        if (UseHtmlString)
        {
            await page.SetContentAsync(htmlDocument, new PageSetContentOptions() { Timeout = 300000 });
        }
        else
        {
            await page.GotoAsync(TargetUrl, new PageGotoOptions() { Timeout = 0 });
        }


        // 使用 css selector 找到所有股票資訊
        var nodes = await page.QuerySelectorAllAsync("li[class='List(n)']");

        var stocks = nodes.Select(async a => new Stock
        {
            //使用 XPath 找到股票名稱、代號、價格、漲跌、漲跌幅、開盤、昨收、最高、最低、成交量
            Name = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[1]/div[2]/div/div[1]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            Symbol = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[1]/div[2]/div/div[2]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            Price = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[2]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            Change = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[3]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            PriceChange =
                (await (await a.EvaluateHandleAsync(
                        "el => document.evaluate('./div/div[4]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                    .AsElement()!.InnerTextAsync()).Trim(),
            Open = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[5]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            LastClose = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[6]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            High = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[7]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            Low = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[8]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            Turnover = (await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[9]',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.InnerTextAsync()).Trim(),
            UpDown = ((await (await a.EvaluateHandleAsync(
                    "el => document.evaluate('./div/div[3]/span',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue"))
                .AsElement()!.GetAttributeAsync("class"))!).Trim()
            //UpDownCheck((await (await a.EvaluateHandleAsync("el => document.evaluate('./div/div[3]/span',el,null,XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue")).AsElement().GetAttributeAsync("class")).Trim()),
        }).Select(b => b.GetAwaiter().GetResult());

        if (ShowDebug)
        {
            foreach (var stock in stocks)
            {
                Console.WriteLine(
                    $"股票名稱: {stock.Name,-12}\t 股票代號: {stock.Symbol}\t 股價: {stock.Price,-5}\t 漲跌: {stock.UpDown} {stock.PriceChange,-8}\t 漲跌幅: {stock.UpDown} {stock.Change,-8}\t 開盤: {stock.Open}\t 昨收: {stock.LastClose}\t 最高: {stock.High}\t 最低: {stock.Low}\t 成交量(張): {stock.Turnover}");
            }
        }
    }
}


class Stock
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public string Price { get; set; }
    public string Change { get; set; }
    public string PriceChange { get; set; }
    public string Open { get; set; }
    public string LastClose { get; set; }
    public string High { get; set; }
    public string Low { get; set; }

    public string Turnover { get; set; }

    //public string UpDown { get; set; }
    private string _upDown;

    public string UpDown
    {
        get => _upDown;
        set => _upDown = UpDownCheck(value);
    }

    string UpDownCheck(string value)
    {
        if (value.Contains("up"))
        {
            return "上漲";
        }

        if (value.Contains("down"))
        {
            return "下跌";
        }

        return string.Empty;
    }
}