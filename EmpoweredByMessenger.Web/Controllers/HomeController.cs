using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace EmpoweredByMessenger.Web.Controllers
{
    public class HomeController : Controller
    {
        private string connectionString = "DefaultEndpointsProtocol=https;AccountName=empoweredbymessenger;AccountKey=Gu5320FoxKIhTLGKLar3KKP+JuTfLi57pnZOxtm3gPqZmX7JVatKNn6daTVcSNbbOSzDGoOImLmKDSQ7fFh5jg==";

        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }

        public ActionResult Feed()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("messages");

            var model = container.ListBlobs().Select(x => x.Uri).ToList();

            return View(model);
        }
    }
}
