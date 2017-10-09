using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FacePayTools
{
    class Program
    {
        static void Main(string[] args)
        {

            MakeRequest();
            Console.WriteLine("Hit ENTER to exit...");
            Console.ReadLine();
        }


        static async void MakeRequest()
        {

            var client = new HttpClient();
            string responseContent;
            var queryString = "";
            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "575223f6ffda4f03b73dc9c8a5cc4a29");
            var uri = "https://southeastasia.api.cognitive.microsoft.com/face/v1.0/facelists" + queryString;
            var response = await client.GetAsync(uri);
            responseContent = response.Content.ReadAsStringAsync().Result;

            JArray jsonObj = JArray.Parse(responseContent);
            foreach (JObject jObject in jsonObj)
            {
                // 干掉facelist
                deletefacelist(jObject["faceListId"].ToString());
                Console.WriteLine(jObject["faceListId"].ToString());
                Thread.Sleep(3000);
            }



        }

        static async void deletefacelist(string facelistid)
        {
            var client = new HttpClient();
            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "575223f6ffda4f03b73dc9c8a5cc4a29");
            var uri = string.Format("https://southeastasia.api.cognitive.microsoft.com/face/v1.0/facelists/{0}", facelistid);
            var response = await client.DeleteAsync(uri);
        }



    }
}
