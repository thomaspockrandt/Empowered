using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using EmpoweredByMessenger.Web.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;
using Newtonsoft.Json.Linq;

namespace EmpoweredByMessenger.Web.Controllers
{
    public class WebhookController : ApiController
    {
        private string connectionString = "DefaultEndpointsProtocol=https;AccountName=empoweredbymessenger;AccountKey=Gu5320FoxKIhTLGKLar3KKP+JuTfLi57pnZOxtm3gPqZmX7JVatKNn6daTVcSNbbOSzDGoOImLmKDSQ7fFh5jg==";

        private string pageToken = "EAAYZBGkaVmKABAN7m2lB3bxNvGFhs7tRC3lUHWYcH3y7Nz2E6nGZBBmb0mmQeOoH2xmnXJTkmgx2tWXooZAlZAzjHXD2AREZBWq6NVwbDTkWNA348WhBDzkWDD3mF7qAiql1JtFZAk0J8oU3RZCWLZC8yapS89KR7uWZATZC9cK5poRgZDZD";

        public HttpResponseMessage Get()
        {
            var querystrings = Request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value);
            if (querystrings["hub.verify_token"] == "hello")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(querystrings["hub.challenge"], Encoding.UTF8, "text/plain")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody]WebhookModel value)
        {
            var comparison = StringComparison.InvariantCultureIgnoreCase;

            if (value._object != "page")
                return new HttpResponseMessage(HttpStatusCode.OK);

            foreach (var item in value.entry[0].messaging)
            {
                if (item.message == null && item.postback == null) continue;

                if (item?.message?.text?.IndexOf("hi", comparison) > -1 ||
                    item?.message?.text?.IndexOf("hello", comparison) > -1)
                {
                    var message = "Hello 🙂 Tell me your story and let me know if you want someone to have a look 📋";
                    await SendMessage(GetMessageTemplate(message, item.sender.id));
                }
                else if (item?.message?.text?.IndexOf("review", comparison) > -1 ||
                    item?.message?.text?.IndexOf("have a look", comparison) > -1)
                {
                    var message = "Of course, I will contact the support network. Don't worry, you will stay annonymous.";
                    await SendMessage(GetMessageTemplate(message, item.sender.id));

                    await Task.Factory.StartNew(async () =>
                    {
                        await Task
                            .Delay(TimeSpan.FromSeconds(5))
                            .ContinueWith(async x =>
                            {
                                await SendMessage(GetMessageTemplate("Your NGO reviewed you files and would like to get in touch.", item.sender.id));
                                await SendMessage(GetImageMessageTemplate("http://empoweredbymessenger.azurewebsites.net/images/rainn.jpg", item.sender.id));
                                await SendMessage(GetMessageTemplate("Remember, you can stay annonymous and still get help.", item.sender.id));
                                await SendMessage(GetMessageTemplate("Here is your report: http://empoweredbymessenger.azurewebsites.net/home/feed", item.sender.id));
                            });
                    });
                }
                else
                {
                    UploadMessageToBlobStorage(item);
                    if (!string.IsNullOrEmpty(item.message?.text))
                    {
                        if (DateTime.Now.Second % 5 == 0)
                            await SendMessage(GetMessageTemplate("I understand, please feel free share more.", item.sender.id));
                    }
                    else if (DateTime.Now.Second % 4 == 0)
                        await SendMessage(GetMessageTemplate("Got it, it's safe.", item.sender.id));
                    else if (DateTime.Now.Second % 3 == 0)
                        await SendMessage(GetMessageTemplate("Received and stored.", item.sender.id));
                    else
                        await SendMessage(GetMessageTemplate("Thanks, added.", item.sender.id));
                }

                if (item?.message?.text?.IndexOf("thanks", comparison) > -1 ||
                    item?.message?.text?.IndexOf("thx", comparison) > -1)
                {
                    var message = "No worries 🙂";
                    await SendMessage(GetMessageTemplate(message, item.sender.id));
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private void UploadMessageToBlobStorage(Messaging item)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("messages");

            string content;
            string blobName = item.timestamp + ".txt";
            if (item.message != null && item.message.attachments != null)
            {
                content = item.message.attachments[0].payload.url;
            }
            else
                content = item.message.text;

            var bytes = Encoding.UTF8.GetBytes(content);
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
            blockBlob.UploadFromByteArray(bytes, 0, bytes.Length);

            var serviceProperties = blobClient.GetServiceProperties();
            serviceProperties.Cors = new CorsProperties();
            serviceProperties.Cors.CorsRules.Add(new CorsRule()
            {
                AllowedHeaders = new List<string> { "*" },
                AllowedMethods = CorsHttpMethods.Put | CorsHttpMethods.Get | CorsHttpMethods.Head | CorsHttpMethods.Post,
                AllowedOrigins = new List<string> { "*" },
                ExposedHeaders = new List<string> { "*" },
                MaxAgeInSeconds = 600
            });
            blobClient.SetServiceProperties(serviceProperties);
        }

        /// <summary>
        /// get text message template
        /// </summary>
        /// <param name="text">text</param>
        /// <param name="sender">sender id</param>
        /// <returns>json</returns>
        private JObject GetMessageTemplate(string text, string sender)
        {
            return JObject.FromObject(new
            {
                recipient = new { id = sender },
                message = new { text = text }
            });
        }

        private JObject GetImageMessageTemplate(string imageUrl, string sender)
        {
            return JObject.FromObject(new
            {
                recipient = new { id = sender },
                message = new { attachment = new { type = "image", payload = new { url = imageUrl } } }
            });
        }
        
        /// <summary>
        /// send message
        /// </summary>
        /// <param name="json">json</param>
        private async Task SendMessage(JObject json)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var res = await client.PostAsync($"https://graph.facebook.com/v2.6/me/messages?access_token={pageToken}", new StringContent(json.ToString(), Encoding.UTF8, "application/json"));
            }
        }
    }
}
