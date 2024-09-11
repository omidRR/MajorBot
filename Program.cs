using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Web;

class Program
{
    static List<UrlInfo> urls;
    static List<Timer> timers;
    static List<RequestInfo> requestInfos = new List<RequestInfo>();
    static Random random = new Random();
    static List<Timer> claimTimers;
    static List<Timer> TimerVisit;
    static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
    static async Task Main(string[] args)
    {
        try
        {
            var filePath = Path.Combine(currentDirectory, "urls.txt");

            Console.WriteLine("Development by https://github.com/omidRR/Major");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "");
                Console.WriteLine("input Url token in 'urls.txt'");
            }

            urls = File.ReadAllLines(filePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => new UrlInfo { Path = line, Token = "" })
                .ToList();

            timers = new List<Timer>();
            claimTimers = new List<Timer>();
            TimerVisit = new List<Timer>();
            await InitTokens();

            var requestTimer = new Timer(async _ => await InitTokens(), null, TimeSpan.FromHours(4), TimeSpan.FromHours(4));
            timers.Add(requestTimer);
            foreach (var url in urls)
            {
                Thread.Sleep(1500);
                Uri uri;
                bool isValidUri = Uri.TryCreate(url.Path, UriKind.Absolute, out uri);

                string queryData;
                if (isValidUri && uri.Fragment.Contains("tgWebAppData"))
                {
                    var query = HttpUtility.ParseQueryString(uri.Fragment.TrimStart('#'));
                    queryData = query["tgWebAppData"];
                }
                else if (isValidUri)
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    queryData = uri.Query;
                }
                else
                {
                    queryData = url.Path;
                }

                var decodedData = HttpUtility.UrlDecode(queryData);
                var keyValuePairs = HttpUtility.ParseQueryString(decodedData);
                var userDataJson = keyValuePairs["user"];

                if (string.IsNullOrEmpty(userDataJson))
                {
                    LogError($"Invalid user data in URL: {url.Path}");
                    continue;
                }

                var userData = JObject.Parse(userDataJson);
                var devAuthData = (long)userData["id"];
                var firstName = (string)userData["first_name"];
                var accountInfo = $"[{devAuthData}_{firstName}]";




                var visitTimer = new Timer(async _ => await SendVisitRequest(url, accountInfo), null, TimeSpan.Zero, TimeSpan.FromHours(3));
                TimerVisit.Add(visitTimer);

                var claimTimer = new Timer(async _ => await SendRouletteRequest(url, accountInfo), null, TimeSpan.Zero, TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(2)));
                claimTimers.Add(claimTimer);


                var coinsTimer = new Timer(async _ => await SendCoinsRequest(url, accountInfo), null, TimeSpan.Zero, TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(5)));
                claimTimers.Add(coinsTimer);

                var SwipeCoinsTimer = new Timer(async _ => await SendSwipeCoinsRequest(url, accountInfo), null, TimeSpan.Zero, TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(7)));
                claimTimers.Add(SwipeCoinsTimer);


                var PavelCoinsTimer = new Timer(async _ => await SendPavelCoinsRequest(url, accountInfo), null, TimeSpan.Zero, TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(20)));
                claimTimers.Add(PavelCoinsTimer);



                await JoinSquadRequest(url, accountInfo);
            }

            Console.ReadLine();
        }
        catch (Exception ex)
        {
            LogError($"An error occurred in Main: {ex.Message}");
        }
    }

    static async Task InitTokens()
    {
        try
        {
            var tasks = urls.Select(async url =>
            {
                try
                {
                    Thread.Sleep(4000);
                    await SendRequest(url);
                }
                catch (Exception ex)
                {
                    LogError($"An error occurred while processing URL: {url.Path}. Error: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            LogError($"An error occurred in InitTokens: {ex.Message}");
        }
    }



    static async Task SendRequest(UrlInfo url)
    {
        try
        {
            Thread.Sleep(2000);
            Uri uri;

            bool isValidUri = Uri.TryCreate(url.Path, UriKind.Absolute, out uri);

            string queryData;

            if (isValidUri && uri.Fragment.Contains("tgWebAppData"))
            {
                var query = HttpUtility.ParseQueryString(uri.Fragment.TrimStart('#'));
                queryData = query["tgWebAppData"];
            }
            else if (isValidUri)
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                queryData = uri.Query;
            }
            else
            {
                queryData = url.Path;
            }

            var decodedData = HttpUtility.UrlDecode(queryData);
            var keyValuePairs = HttpUtility.ParseQueryString(decodedData);
            var userDataJson = keyValuePairs["user"];

            if (string.IsNullOrEmpty(userDataJson))
            {
                LogError($"No user data found in input: {url.Path}");
                return;
            }

            var userData = JObject.Parse(userDataJson);
            var devAuthData = (long)userData["id"];
            var firstName = (string)userData["first_name"];
            var accountInfo = $"[{devAuthData}_{firstName}]";

            var tokenFilePath = Path.Combine(currentDirectory, "tokens.json");
            string accountKey = devAuthData.ToString();

            string token = GetStoredToken(tokenFilePath, accountKey);
            if (string.IsNullOrEmpty(token))
            {
                Thread.Sleep(2000);
                token = await GetNewToken(queryData, accountInfo);
                SaveToken(tokenFilePath, accountKey, token);
            }

            url.Token = token;

            bool success = await SendSecondRequest(token, accountInfo);
            if (!success)
            {
                token = await GetNewToken(queryData, accountInfo);
                SaveToken(tokenFilePath, accountKey, token);

                url.Token = token;
                Thread.Sleep(2000);
                await SendSecondRequest(token, accountInfo);
            }
            Thread.Sleep(2000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }




    static string GetStoredToken(string filePath, string accountKey)
    {

        if (!File.Exists(filePath))
        {
            return null;
        }

        var tokens = JObject.Parse(File.ReadAllText(filePath));
        return tokens[accountKey]?.ToString();
    }

    static void SaveToken(string filePath, string accountKey, string token)
    {
        JObject tokens;

        if (File.Exists(filePath))
        {
            tokens = JObject.Parse(File.ReadAllText(filePath));
        }
        else
        {
            tokens = new JObject();
        }

        tokens[accountKey] = token;
        File.WriteAllText(filePath, tokens.ToString());
    }

    static async Task<string> GetNewToken(string tgWebAppData, string accountInfo)
    {
        try
        {
            Thread.Sleep(2000);
            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/auth/tg/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9,fa;q=0.8");
            request.AddHeader("content-type", "application/json");
            request.AddHeader("cookie", "SL_G_WPT_TO=fa; SL_GWPT_Show_Hide_tmp=1; SL_wptGlobTipTmp=1");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://major.glados.app");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Google Chrome\";v=\"127\", \"Chromium\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");
            request.AddHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");

            var body = new
            {
                init_data = tgWebAppData
            };

            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[ErrorGetnewToken]==>request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[ErrorGetnewToken]==>im Trying Again...");
                Thread.Sleep(5000);
                await GetNewToken(tgWebAppData, accountInfo);
            }
            if (response.IsSuccessful)
            {
                Console.WriteLine($"{accountInfo}==>Token received successfully.");
                var jsonResponse = JObject.Parse(response.Content);
                var accessToken = jsonResponse["access_token"].ToString();
                Console.WriteLine($"{accountInfo}==>Access Token: {accessToken}");

                return accessToken;
            }
            else
            {

                Console.WriteLine($"{accountInfo}==>Request failed with status code: {response.StatusCode}");
                Console.WriteLine(response.Content);
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return null;
        }
    }
    static async Task<bool> SendSecondRequest(string token, string accountInfo)
    {
        try
        {
            Thread.Sleep(2000);
            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/tasks/?is_daily=true", Method.Get);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9");
            request.AddHeader("authorization", $"Bearer {token}");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/earn");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Microsoft Edge\";v=\"127\", \"Chromium\";v=\"127\", \"Microsoft Edge WebView2\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");
            request.AddHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0");

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[Visit]==>Visit request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[ErrorVisit]==>im Trying Again...");
                Thread.Sleep(5000);
                await SendSecondRequest(token, accountInfo);
            }

            if (response.IsSuccessful)
            {
                Console.WriteLine($"{accountInfo}==>Task list received successfully.");
                var jsonResponse = JArray.Parse(response.Content);

                string[] taskTitles = await DownloadTaskTitles("https://raw.githubusercontent.com/chitoz1300/REXBOT/main/Major.txt");

                foreach (var title in taskTitles)
                {
                    var task = jsonResponse.FirstOrDefault(t => t["title"].ToString() == title);
                    if (task != null)
                    {
                        var taskId = task["id"].ToString();
                        Console.WriteLine($"{accountInfo}==>Found task ID for '{title}': {taskId}");

                        await SendTaskRequest(token, taskId, accountInfo);
                    }
                    else
                    {
                        Console.WriteLine($"{accountInfo}==>Task '{title}' not found.");
                    }
                }

                return true;
            }
            else
            {
                Console.WriteLine($"{accountInfo}==>Second request failed with status code: {response.StatusCode}");
                Console.WriteLine(response.Content);
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}==>An error occurred in the second request: {ex.Message}");
            return false;
        }
    }

    static async Task<string[]> DownloadTaskTitles(string url)
    {
        using (HttpClient httpClient = new HttpClient())
        {
            var content = await httpClient.GetStringAsync(url);
            return content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    static async Task SendTaskRequest(string token, string taskId, string accountInfo)
    {
        try
        {
            Thread.Sleep(2000);

            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/tasks/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9");
            request.AddHeader("authorization", $"Bearer {token}");
            request.AddHeader("content-type", "application/json");
            request.AddHeader("origin", "https://major.glados.app");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/earn");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Microsoft Edge\";v=\"127\", \"Chromium\";v=\"127\", \"Microsoft Edge WebView2\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");
            request.AddHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0");

            var body = new
            {
                task_id = taskId
            };

            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[ErrorSendTask]==>request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[ErrorSendTask]==>im Trying Again...");
                Thread.Sleep(5000);
                await SendTaskRequest(token, taskId, accountInfo);
                return;
            }
            if (response.Content.Contains("Not Found"))
            {
                Console.WriteLine($"{accountInfo}[[SendTask]]==>request failed with status code: {response.StatusCode}");
                return;
            }
            if (response.IsSuccessful)
            {
                Console.WriteLine($"{accountInfo}[SendTask]==>Task '{taskId}' successfully triggered.");
                Console.WriteLine(response.Content);
            }
            else
            {

                Console.WriteLine($"{accountInfo}[ErrorSendTask]==>request failed: {response.StatusCode} ==> Response==>{response.Content}");
                Console.WriteLine(response.Content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}==>An error occurred in the task request: {ex.Message}");
        }
    }
    static async Task SendRouletteRequest(UrlInfo url, string accountInfo)
    {
        try
        {
            Thread.Sleep(2000);

            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/roulette/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9,fa;q=0.8");
            request.AddHeader("authorization", $"Bearer {url.Token}");
            request.AddHeader("content-length", "0");
            request.AddHeader("cookie", "SL_G_WPT_TO=fa; SL_GWPT_Show_Hide_tmp=1; SL_wptGlobTipTmp=1");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://major.glados.app");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/reward");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Google Chrome\";v=\"127\", \"Chromium\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");
            request.AddHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[ErrorSendRoulette]==>request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[ErrorSendRoulette]==>im Trying Again...");
                Thread.Sleep(5000);
                await SendRouletteRequest(url, accountInfo);
                return;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"{accountInfo}[Visit]==>Unauthorized, attempting to refresh token...");

                string newToken = await GetNewTokenFromRequest(url);

                if (newToken != null)
                {
                    url.Token = newToken;

                    request.AddOrUpdateHeader("authorization", $"Bearer {url.Token}");

                    response = await client.ExecuteAsync(request);
                }
            }
            if (response.IsSuccessful)
            {
                var jsonResponse = JObject.Parse(response.Content);

                if (jsonResponse["rating_award"] != null)
                {
                    int ratingAward = jsonResponse["rating_award"].Value<int>();
                    Console.WriteLine($"{accountInfo}==>Rating Award: {ratingAward}");
                }

            }
            else if (response.IsSuccessful is false)
            {
                if (response.Content is null || response.Content == "")
                {
                    Console.WriteLine($"{accountInfo}==>Error: {response.Content}");
                    return;
                }
                if (response.Content.Contains("Not Found"))
                {
                    Console.WriteLine($"{accountInfo}[ErrorSendRouletteNOTFOUND!]==>request failed with status code: {response.StatusCode}");
                    return;
                }

                if (response.Content.Contains("blocked_until"))
                {
                    var jsonResponse = JObject.Parse(response.Content);

                    if (jsonResponse["detail"] != null && jsonResponse["detail"]["blocked_until"] != null)
                    {
                        double blockedUntil = jsonResponse["detail"]["blocked_until"].Value<double>();

                        DateTime blockedTime = DateTimeOffset.FromUnixTimeSeconds((long)blockedUntil).DateTime;

                        TimeSpan waitTime = blockedTime - DateTime.UtcNow + TimeSpan.FromMinutes(5);
                        Console.WriteLine(
                            $"{accountInfo}==>Need to wait until: {blockedTime} (Adding 5 minutes extra). Total wait: {waitTime.TotalMinutes} minutes");

                        await Task.Delay(waitTime);
                        await SendRouletteRequest(url, accountInfo);
                    }
                }
            }
            else
            {
                Console.WriteLine($"{accountInfo}==>Roulette request failed with status code: {response.StatusCode}");
                Console.WriteLine(response.Content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}==>An error occurred in the roulette request: {ex.Message}");
        }
    }
    static async Task SendCoinsRequest(UrlInfo url, string accountInfo)
    {
        try
        {
            Thread.Sleep(2000);

            var randomCoins = random.Next(890, 914);

            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/bonuses/coins/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9,fa;q=0.8");
            request.AddHeader("authorization", $"Bearer {url.Token}");
            request.AddHeader("content-type", "application/json");
            request.AddHeader("cookie", "SL_G_WPT_TO=fa; SL_GWPT_Show_Hide_tmp=1; SL_wptGlobTipTmp=1");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://major.glados.app");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/reward");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Google Chrome\";v=\"127\", \"Chromium\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");

            var body = new
            {
                coins = randomCoins
            };

            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[ErrorCoin]==>request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[ErrorCoin]==>im Trying Again...");
                Thread.Sleep(5000);
                await SendCoinsRequest(url, accountInfo);
                return;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"{accountInfo}[Visit]==>Unauthorized, attempting to refresh token...");

                string newToken = await GetNewTokenFromRequest(url);

                if (newToken != null)
                {
                    url.Token = newToken;

                    request.AddOrUpdateHeader("authorization", $"Bearer {url.Token}");

                    response = await client.ExecuteAsync(request);
                }
            }
            if (response.Content.Contains("Not Found"))
            {
                Console.WriteLine($"{accountInfo}[ErrorSendRouletteNOTFOUND!]==>request failed with status code: {response.StatusCode}");
                return;
            }
            if (response.IsSuccessful)
            {
                var jsonResponse = JObject.Parse(response.Content);

                Console.WriteLine($"{accountInfo}==>Coins request succeeded with {randomCoins} coins.");
            }
            else if (response.IsSuccessful is false)
            {
                if (response.Content.Contains("blocked_until"))
                {
                    var jsonResponse = JObject.Parse(response.Content);

                    if (jsonResponse["detail"] != null && jsonResponse["detail"]["blocked_until"] != null)
                    {
                        double blockedUntil = jsonResponse["detail"]["blocked_until"].Value<double>();


                        DateTime blockedTime = DateTimeOffset.FromUnixTimeSeconds((long)blockedUntil).DateTime;


                        TimeSpan waitTime = blockedTime - DateTime.UtcNow + TimeSpan.FromMinutes(5);
                        Console.WriteLine(
                            $"{accountInfo}==>Need to wait until: {blockedTime} (Adding 5 minutes extra). Total wait: {waitTime.TotalMinutes} minutes");


                        await Task.Delay(waitTime);
                        await SendCoinsRequest(url, accountInfo);
                    }
                }
            }
            else
            {

                Console.WriteLine($"{accountInfo}==>Coins request failed with status code: {response.StatusCode}");
                Console.WriteLine(response.Content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}==>An error occurred in the coins request: {ex.Message}");
        }
    }
    static async Task SendSwipeCoinsRequest(UrlInfo url, string accountInfo)
    {
        try
        {
            Thread.Sleep(2000);
            var randomCoins = random.Next(2880, 2980);
            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/swipe_coin/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9,fa;q=0.8");
            request.AddHeader("authorization", $"Bearer {url.Token}");
            request.AddHeader("content-type", "application/json");
            request.AddHeader("cookie", "SL_G_WPT_TO=fa; SL_GWPT_Show_Hide_tmp=1; SL_wptGlobTipTmp=1");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://major.glados.app");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/reward");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Google Chrome\";v=\"127\", \"Chromium\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");

            var body = new
            {
                coins = randomCoins
            };

            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[SwipeCoins]==>request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[SwipeCoins]==>im Trying Again...");
                Thread.Sleep(5000);
                await SendSwipeCoinsRequest(url, accountInfo);
                return;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"{accountInfo}[SwipeCoins]==>Unauthorized, attempting to refresh token...");

                string newToken = await GetNewTokenFromRequest(url);

                if (newToken != null)
                {
                    url.Token = newToken;

                    request.AddOrUpdateHeader("authorization", $"Bearer {url.Token}");

                    response = await client.ExecuteAsync(request);
                }
            }
            if (response.Content.Contains("Not Found"))
            {
                Console.WriteLine($"{accountInfo}[SwipeCoins!]==>request failed with status code: {response.StatusCode}");
                return;
            }
            if (response.IsSuccessful)
            {
                var jsonResponse = JObject.Parse(response.Content);

                Console.WriteLine($"{accountInfo}[SwipeCoins]==>==>Coins request succeeded with {randomCoins} coins.");
            }
            else if (response.IsSuccessful is false)
            {
                if (response.Content.Contains("blocked_until"))
                {
                    var jsonResponse = JObject.Parse(response.Content);

                    if (jsonResponse["detail"] != null && jsonResponse["detail"]["blocked_until"] != null)
                    {
                        double blockedUntil = jsonResponse["detail"]["blocked_until"].Value<double>();


                        DateTime blockedTime = DateTimeOffset.FromUnixTimeSeconds((long)blockedUntil).DateTime;


                        TimeSpan waitTime = blockedTime - DateTime.UtcNow + TimeSpan.FromMinutes(5);
                        Console.WriteLine(
                            $"{accountInfo}[SwipeCoins]==>==>Need to wait until: {blockedTime} (Adding 5 minutes extra). Total wait: {waitTime.TotalMinutes} minutes");


                        await Task.Delay(waitTime);
                        await SendSwipeCoinsRequest(url, accountInfo);
                    }
                }
            }
            else
            {

                Console.WriteLine($"{accountInfo}==>[SwipeCoins]==>Coins request failed with status code: {response.StatusCode}");
                Console.WriteLine(response.Content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}[SwipeCoins]==>==>An error occurred in the coins request: {ex.Message}");
        }
    }

    static async Task SendPavelCoinsRequest(UrlInfo url, string accountInfo)
    {
        try
        {
            Thread.Sleep(2000);

            var jsonData = await GetJsonFromUrl("https://raw.githubusercontent.com/chitoz1300/REXBOT/main/Pavelmajor.json");

            var choices = jsonData.ToObject<Dictionary<string, int>>();

            var client = new RestClient("https://major.bot");
            var request = new RestRequest("/api/durov/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9,fa;q=0.8");
            request.AddHeader("authorization", $"Bearer {url.Token}");
            request.AddHeader("content-type", "application/json");
            request.AddHeader("cookie", "SL_G_WPT_TO=fa; SL_GWPT_Show_Hide_tmp=1; SL_wptGlobTipTmp=1");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://major.bot");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.bot/games/puzzle-durov");
            request.AddHeader("sec-ch-ua", "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\", \"Google Chrome\";v=\"128\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");
            request.AddHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");

            request.AddJsonBody(choices);

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[PavelCoins]==>request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[PavelCoins]==>im Trying Again...");
                Thread.Sleep(5000);
                await SendPavelCoinsRequest(url, accountInfo);
                return;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"{accountInfo}[PavelCoins]==>Unauthorized, attempting to refresh token...");

                string newToken = await GetNewTokenFromRequest(url);

                if (newToken != null)
                {
                    url.Token = newToken;

                    request.AddOrUpdateHeader("authorization", $"Bearer {url.Token}");

                    response = await client.ExecuteAsync(request);
                }
            }
            if (response.Content.Contains("Not Found"))
            {
                Console.WriteLine($"{accountInfo}[PavelCoins!]==>request failed with status code: {response.StatusCode}");
                return;
            }
            if (response.IsSuccessful)
            {
                var jsonResponse = JObject.Parse(response.Content);
                Console.WriteLine($"{accountInfo}[PavelCoins]==>Coins request succeeded.");
            }
            else if (response.IsSuccessful is false)
            {

                if (response.Content.Contains("blocked_until"))
                {

                    var jsonResponse = JObject.Parse(response.Content);

                    if (jsonResponse["detail"] != null && jsonResponse["detail"]["blocked_until"] != null)
                    {
                        double blockedUntil = jsonResponse["detail"]["blocked_until"].Value<double>();


                        DateTime blockedTime = DateTimeOffset.FromUnixTimeSeconds((long)blockedUntil).DateTime;


                        TimeSpan waitTime = blockedTime - DateTime.UtcNow + TimeSpan.FromMinutes(5);
                        Console.WriteLine(
                            $"{accountInfo}[PavelCoins]==>==>Need to wait until: {blockedTime} (Adding 5 minutes extra). Total wait: {waitTime.TotalMinutes} minutes");


                        await Task.Delay(waitTime);
                        await SendPavelCoinsRequest(url, accountInfo);
                    }
                }
            }
            else
            {
                Console.WriteLine($"{accountInfo}[PavelCoins]==>Coins request failed with status code: {response.StatusCode}");
                Console.WriteLine(response.Content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}[PavelCoins]==>An error occurred in the coins request: {ex.Message}");
        }
    }

    static async Task<JObject> GetJsonFromUrl(string url)
    {
        using (var httpClient = new HttpClient())
        {
            var response = await httpClient.GetStringAsync(url);
            return JObject.Parse(response);
        }
    }


    static async Task JoinSquadRequest(UrlInfo url, string accountInfo)
    {
        try
        {
            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/squads/1397368454/join/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9,fa;q=0.8");
            request.AddHeader("authorization", $"Bearer {url.Token}");
            request.AddHeader("content-type", "application/json");
            request.AddHeader("cookie", "SL_G_WPT_TO=fa; SL_GWPT_Show_Hide_tmp=1; SL_wptGlobTipTmp=1");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://major.glados.app");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/squad/1397368454");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Google Chrome\";v=\"127\", \"Chromium\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");

            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                Console.WriteLine($"{accountInfo}==>Successfully joined the squad.");
            }
            else
            {
                Console.WriteLine($"{accountInfo}==>: {response.StatusCode} + : Response : {response.Content} ");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}==>An error occurred while trying to join the squad: {ex.Message}");
        }
    }

    static async Task SendVisitRequest(UrlInfo url, string accountInfo)
    {
        try
        {
            Thread.Sleep(3000);

            var client = new RestClient("https://major.glados.app");
            var request = new RestRequest("/api/user-visits/visit/", Method.Post);

            request.AddHeader("accept", "application/json, text/plain, */*");
            request.AddHeader("accept-language", "en-US,en;q=0.9,fa;q=0.8");
            request.AddHeader("authorization", $"Bearer {url.Token}");
            request.AddHeader("content-type", "application/json");
            request.AddHeader("cookie", "SL_G_WPT_TO=fa; SL_GWPT_Show_Hide_tmp=1; SL_wptGlobTipTmp=1");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://major.glados.app");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://major.glados.app/reward");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"99\", \"Google Chrome\";v=\"127\", \"Chromium\";v=\"127\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-origin");

            var response = await client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"{accountInfo}[Visit]==>Visit request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[ErrorVisit]==>im Trying Again...");
                Thread.Sleep(5000);
                await SendVisitRequest(url, accountInfo);
                return;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"{accountInfo}[Visit]==>Unauthorized, attempting to refresh token...");
                Thread.Sleep(2000);
                string newToken = await GetNewTokenFromRequest(url);

                if (newToken != null)
                {
                    url.Token = newToken;

                    request.AddOrUpdateHeader("authorization", $"Bearer {url.Token}");

                    response = await client.ExecuteAsync(request);
                }
            }
            if (response.Content.Contains("Not Found"))
            {
                Console.WriteLine($"{accountInfo}[ErrorSendRouletteNOTFOUND!]==>request failed with status code: {response.StatusCode}");
                return;
            }
            if (response.IsSuccessful)
            {
                Console.WriteLine($"{accountInfo}[OkVisit]==>{response.Content}");
            }
            else
            {

                Console.WriteLine($"{accountInfo}[Visit]==>Visit request failed with status code: {response.StatusCode}");
                Console.WriteLine($"{accountInfo}[ErrorVisit]==>{response.Content}");
            }
            Thread.Sleep(2000);
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{accountInfo}[ErrorVisit]==>An error occurred during the visit request: {ex.Message}");
        }
    }

    static async Task<string> GetNewTokenFromRequest(UrlInfo url)
    {
        try
        {
            await SendRequest(url);
            return url.Token;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while refreshing token: {ex.Message}");
            return null;
        }
    }
    static void LogError(string message)
    {
        var logFilePath = Path.Combine(currentDirectory, "log.txt");
        var logMessage = $"[ERROR] => {message}";
        lock (requestInfos)
        {
            File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
        }
    }

    class UrlInfo
    {
        public string Path { get; set; }
        public string Token { get; set; }
    }

    class RequestInfo
    {
        public UrlInfo Url { get; set; }
        public string FirstName { get; set; }
        public DateTime NextRequestTime { get; set; }
    }
}