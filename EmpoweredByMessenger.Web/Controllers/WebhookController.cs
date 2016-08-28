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
            if (value._object != "page")
                return new HttpResponseMessage(HttpStatusCode.OK);

            foreach (var item in value.entry[0].messaging)
            {
                if (item.message == null && item.postback == null)
                    continue;
                else
                {
                    UploadMessageToBlobStorage(item);
                    await SendMessage(GetMessageTemplate(item.message.text, item.sender.id));
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
                AllowedHeaders = new List<string>() { "*" },
                AllowedMethods = CorsHttpMethods.Put | CorsHttpMethods.Get | CorsHttpMethods.Head | CorsHttpMethods.Post,
                AllowedOrigins = new List<string>() { "*" },
                ExposedHeaders = new List<string>() { "*" },
                MaxAgeInSeconds = 600 // 30 minutes
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

        /// <summary>
        /// send message
        /// </summary>
        /// <param name="json">json</param>
        private async Task SendMessage(JObject json)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage res = await client.PostAsync($"https://graph.facebook.com/v2.6/me/messages?access_token={pageToken}", new StringContent(json.ToString(), Encoding.UTF8, "application/json"));
            }
        }
    }
}
