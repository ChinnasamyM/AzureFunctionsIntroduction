using System.Net;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Text.RegularExpressions;

namespace AzureFunctionsIntroduction
{
    public static class LineBotWebhookCSharp
    {
        private static readonly string lineChannelId = Environment.GetEnvironmentVariable("LineChannelId");
        private static readonly string lineChannelSecret = Environment.GetEnvironmentVariable("LineChannelSecret");
        private static readonly string lineMid = Environment.GetEnvironmentVariable("LineMid");

        [FunctionName("LineBotWebhookCSharp")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"LineBotWebhookCSharp : Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            var result = data.result[0];

            // Debug
            log.Info($"result : {result}");
            log.Info($"data : {data}");

            if (result == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Seems result not contains in json."
                });
            }

            // Create Bot Response
            var content = CreateResponseContent(result, log);

            using (var client = new HttpClient())
            {
                // Set line authorization header
                client.DefaultRequestHeaders.Add("X-Line-ChannelID", lineChannelId);
                client.DefaultRequestHeaders.Add("X-Line-ChannelSecret", lineChannelSecret);
                client.DefaultRequestHeaders.Add("X-Line-Trusted-User-With-ACL", lineMid);

                // Send Response to Line Sender
                var res = await client.PostAsJsonAsync("https://trialbot-api.line.me/v1/events",
                    new
                    {
                        to = new[] { result.content.from },
                        toChannel = "1383378250",
                        // �g138311609000106303�h	Received message (example: text, images)
                        // �g138311609100106403�h	Received operation (example: added as friend)
                        eventType = "138311608800106203",
                        content = content
                    }
                );

                // Response Code
                return req.CreateResponse(res.StatusCode, new
                {
                    text = $"{content.contentType}"
                });
            }
        }

        static Content CreateResponseContent(dynamic result, TraceWriter log)
        {
            var text = result.content.text;
            var contentMetaData = result.content.contentMetadata;
            var location = result.content.location;
            Content content = null;

            if (location != null)
            {
                string latitude = location.latitude;
                string longitude = location.longitude;
                string address = location.address;
                log.Info($"location. Latitude : {latitude}, Longitude : {longitude}, Address : {address}");
                var responseText = new RescureUrl(latitude, longitude, address).GetAsync().Result;
                content = new Content
                {
                    contentType = 1,
                    toType = 1,
                    text = responseText,
                };
            }
            else if (text != null)
            {
                var responseText = $"�ʒu�������L���Ă����Ƌً}�����𒲂ׂ܂��I";
                log.Info($"message : {responseText}");
                content = new Content
                {
                    contentType = 1,
                    toType = 1,
                    text = responseText,
                };
            }
            else if (contentMetaData.SKTID != "")
            {
                var responseText = $"�ʒu�������L���Ă����Ƌً}�����𒲂ׂ܂��I";
                log.Info($"message : {responseText}");
                content = new Content
                {
                    contentType = 1,
                    toType = 1,
                    text = responseText,
                };
            }
            return content;
        }

        public class RescureUrl
        {
            // �u�߂��ً̋}����T���܂��v�𗘗p : �����������a3km�ȓ��ŋ߂��̔��������Ă���܂�
            private static readonly string _searchBaseUrl = "https://0312.yanoshin.jp/rescue/index";

            // �u�i�r�^�C���ЊQ���v�𗘗p : kumamoto prefecture ���܂܂�Ă�����F�{�n�k�Ƃ݂Ȃ���URL �ǉ�
            private static readonly string _navitimeUrl = "http://www.navitime.co.jp/saigai/?from=pctop";

            // Google �N���C�V�X���X�|���X�𗘗p : https://www.google.org/crisisresponse/japan
            // Map �\�����ł��邯�ǁA�ʒu�֌W�Ȃ��Ȃ̂ň�UTOP �݂̂�Map�͂Ȃ� : https://www.google.org/crisisresponse/japan/maps?hl=ja
            private static readonly string _googleChrisisUrl = "https://www.google.org/crisisresponse/japan";

            public string Latitude { get; private set; }
            public string Longitude { get; private set; }
            public string Address { get; private set; }
            public bool IsKumamoto => IsKumamotoIncluded(this.Address);

            public RescureUrl(string latitude, string longitude, string address)
            {
                this.Latitude = latitude;
                this.Longitude = longitude;
                this.Address = address;
            }

            public async Task<string> GetAsync()
            {
                var endMessage = new[] {$"�����Ă������������ݒn���͎��̒ʂ�ł��B",
            $"�o�x : {this.Latitude}",
            $"�ܓx : {this.Longitude}",
            $"�Z�� : {this.Address}",
        }.ToJoinedString(Environment.NewLine);
                var requestUrl = new Uri($"{_searchBaseUrl}/{this.Latitude}/{this.Longitude}");
                using (var client = new HttpClient())
                {
                    var res = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (res.IsSuccessStatusCode)
                    {
                        string postMessage = endMessage;
                        if (this.IsKumamoto)
                        {
                            // �F�{���̏Z���������Ă����獷������
                            postMessage = new[] {$"�F�{���̕��ł����H�u�i�r�^�C���ЊQ���v�����Q�l�ɂǂ���",
                        _navitimeUrl,
                        $"{Environment.NewLine}",
                        endMessage,
                    }
                            .ToJoinedString(Environment.NewLine);
                        }

                        var message = new[] {$@"�u�߂��ً̋}����T���܂��v�ŁA�������ʂ�������܂����B",
                    $"�Ŋ��̔���� URL : {requestUrl.AbsoluteUri}",
                    Environment.NewLine,
                    $"���ۏ���Google�N���C�V�X���X�|���X���ǂ��� URL : {_googleChrisisUrl}",
                    Environment.NewLine,
                    postMessage,
                }.ToJoinedString(Environment.NewLine);

                        return message;
                    }
                    return $"�w�肵���Z�����݂���܂���ł����B������x�����Ă��������܂����H{Environment.NewLine}{endMessage}";
                }
            }

            public static bool IsKumamotoIncluded(string address)
            {
                var isEngCultureInvaliant = Regex.IsMatch(address, @"Kumamoto\s*Prefecture", RegexOptions.CultureInvariant);
                var isEngIgnoreCase = Regex.IsMatch(address, @"Kumamoto\s*Prefecture", RegexOptions.IgnoreCase);
                var isEngIgnoreWhitespace = Regex.IsMatch(address, @"Kumamoto\s*Prefecture", RegexOptions.IgnorePatternWhitespace);
                var isJapaneseCultureInvaliat = Regex.IsMatch(address, "�F�{��", RegexOptions.CultureInvariant);
                return isEngCultureInvaliant || isEngIgnoreCase || isEngIgnoreWhitespace || isJapaneseCultureInvaliat;
            }
        }

        public class Content
        {
            public int contentType { get; set; }
            public int toType { get; set; }
            public string text { get; set; }
            public ContentMetadata contentMetadata { get; set; }
        }

        public class ContentMetadata
        {
            public string STKID { get; set; }
            public string STKPKGID { get; set; }
            public string STKVER { get; set; }
        }
    }
}
