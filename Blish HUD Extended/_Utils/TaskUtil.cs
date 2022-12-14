using Flurl.Http;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Blish_HUD.Extended
{
    public class TaskUtil
    {
        private static Logger Logger = Logger.GetLogger<TaskUtil>();
        public static void CallActionWithTimeout(Action action, Action error, int timeout)
        {
            var cancelToken = new CancellationTokenSource();
            var token = cancelToken.Token;
            var task = Task.Run(delegate
            {
                try
                {
                    var thread = Thread.CurrentThread;
                    using (token.Register(thread.Abort))
                    {
                        action();
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }, token);
            var index = Task.WaitAny(task, Task.Delay(timeout));
            if (index != 0)
            {
                cancelToken.Cancel();
                error();
            }
            else if (task.Result != null)
            {
                Logger.Error(task.Result.Message, task.Result);
            }
        }

        public static bool TryParseJson<T>(string json, out T result)
        {
            bool success = true;
            var settings = new JsonSerializerSettings
            {
                Error = (_, args) => { success = false; args.ErrorContext.Handled = true; },
                MissingMemberHandling = MissingMemberHandling.Error
            };
            result = JsonConvert.DeserializeObject<T>(json, settings);
            return success;
        }

        public static async Task<(bool, T)> GetJsonResponse<T>(string request, int timeOutSeconds = 10)
        {
            try
            {
                var rawJson = await request.AllowHttpStatus(HttpStatusCode.NotFound).AllowHttpStatus("200").WithTimeout(timeOutSeconds).GetStringAsync();
                return (TryParseJson<T>(rawJson, out var result), result);
            }
            catch (FlurlHttpTimeoutException ex)
            {
                Logger.Warn(ex, $"Request '{request}' timed out.");
            }
            catch (FlurlHttpException ex)
            {
                Logger.Warn(ex, $"Request '{request}' was not successful.");
            }
            catch (JsonReaderException ex)
            {
                Logger.Warn(ex, $"Failed to deserialize requested content from \"{request}\"\n{ex.StackTrace}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unexpected error while requesting '{request}'.");
            }

            return (false, default);
        }
    }
}
