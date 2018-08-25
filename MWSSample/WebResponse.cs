using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Persistence;

namespace MWSCommon
{

    public class AMZNWebResponse
    {

        public string url_called { get; set; }

        public string response { get; set; }
        public byte[] responseZipped { get; set; }
        public long durationms { get; set; }
        public int StatusCode { get; set; }
        public long ContentLength { get; set; }

        private const string DESTINATION = "mws.amazonservices.com";
        private AmazonAccount _acct;

   

        private HttpClient client = null;


        public AMZNWebResponse(AmazonAccount acct)
        {
            _acct = acct;
            if (client == null)
            {
                client = new HttpClient();
            }

        }





        public string getPostResponse(string serviceURL, IDictionary<string, string> parms, string postData, bool retry)
        {


            AMZNHelper amznHelper = new AMZNHelper(_acct);

            amznHelper.AddRequiredParameters(parms, serviceURL, false);

            Throttle.waitThrottle(parms["Action"], _acct);

           
            //var content2 = HttpUtility.UrlEncode(str, System.Text.Encoding.UTF8);
            var content2 = new FormUrlEncodedContent(parms);
            var requestUrl = serviceURL + "?" + content2.ReadAsStringAsync().Result;
            if (parms["Action"] != null)
            {

                Log.Info(_acct.AmazonAccountId, "calling " + serviceURL + " Action=" + parms["Action"]);

            }

            url_called = requestUrl;



            //var postData = content2.ReadAsStringAsync().Result;

            byte[] dataStream = Encoding.UTF8.GetBytes(postData);

            url_called = requestUrl;

            var start = DateTime.Now;


            string rv;
            Task<HttpResponseMessage> task;

            lock (client)
            {
                client.DefaultRequestHeaders.Clear();

                client.DefaultRequestHeaders.Add("Content-MD5", Convert.ToBase64String(MD5.Create().ComputeHash(UTF8Encoding.UTF8.GetBytes(postData))));

                //client.BaseAddress = new Uri("requestUrl");
                var content = new FormUrlEncodedContent(

                    parms
                );

                task = client.PostAsync(requestUrl, content);
            }

            task.Wait();

            var taskString = task.Result.Content.ReadAsStringAsync();

            taskString.Wait();

            rv = taskString.Result;

            var h = task.Result.Headers;

            try
            {
                if (h.Contains("x-mws-quota-remaining"))
                    Throttle.SetHourlyQuotaHold(parms["Action"], Single.Parse(h.GetValues("x-mws-quota-remaining").First()), DateTime.Parse(h.GetValues("x-mws-quota-resetsOn").First()), _acct);
            }
            catch(Exception e)
            {
                Log.Error(_acct.AmazonAccountId, "Error setting throttle", e);
            }
            if (task.Result.StatusCode != HttpStatusCode.OK)
            {
                if(task.Result.StatusCode == HttpStatusCode.ServiceUnavailable)

                {
                    Log.Warn(_acct.AmazonAccountId, $"Throttled - {parms["Action"]}, waiting 2mins");
                    Thread.Sleep(120000);
                }

                else
                {
                    Log.Warn(_acct.AmazonAccountId, $"Error Calling MWS {parms["Action"]} failed with status {task.Result.StatusCode}");
                }

                if (retry )
                {
                
                    return getPostResponse(serviceURL, parms, postData, false);
                }
                else
                {

                    Log.Warn(_acct.AmazonAccountId, $"Error Calling MWS {requestUrl} failed with status {task.Result.StatusCode}");
                }
               
                return rv;
            }
            return rv;







        }


        public string getResponse(string serviceURL, IDictionary<string, string> parms)
        {
            return getResponse(serviceURL, parms, false);
        }




        public string getResponse(string serviceURL, IDictionary<string, string> parms, bool retry)
        {


            try
            {
                
                Throttle.waitThrottle(parms["Action"], _acct);


                AMZNHelper amznHelper = new AMZNHelper(_acct);


                amznHelper.AddRequiredParameters(parms, serviceURL, true);


                //var content2 = HttpUtility.UrlEncode(str, System.Text.Encoding.UTF8);
                var content2 = new FormUrlEncodedContent(parms);
                var requestUrl = serviceURL + "?" + content2.ReadAsStringAsync().Result;


                url_called = requestUrl;

                var start = DateTime.Now;
                string rv = "";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();

                    //    client.DefaultRequestHeaders.Add("Content-MD5", Convert.ToBase64String(MD5.Create().ComputeHash(UTF8Encoding.UTF8.GetBytes(postData))));

                    //client.BaseAddress = new Uri("requestUrl");
                    var content = new FormUrlEncodedContent(

                        parms
                    );
                    // Log.Error(_acct.AmazonAccountId, $"Requesting async {requestUrl}");
                    var task = client.GetAsync(requestUrl);


                    task.Wait(60000);




                    var bufTask = task.Result.Content.ReadAsByteArrayAsync();
                    var h = task.Result.Headers;

                    if(parms.ContainsKey("Action") && h.Contains("x-mws-quota-remaining") && h.Contains("x-mws-quota-resetsOn"))
                        Throttle.SetHourlyQuotaHold(parms["Action"], Single.Parse(h.GetValues("x-mws-quota-remaining").First()), DateTime.Parse(h.GetValues("x-mws-quota-resetsOn").First()),_acct);

                    bufTask.Wait(5000);

                    var byteArray = bufTask.Result.ToArray();

                    var responseString = Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);

                    var taskString = task.Result.Content.ReadAsStringAsync();

                    rv = responseString;


                    if (task.Result.StatusCode != HttpStatusCode.OK)
                    {
                        Log.Warn(_acct.AmazonAccountId, $"Error Calling MWS {requestUrl} failed message:{rv}  status: {task.Result.StatusCode}");

                        if (retry || task.Result.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            Log.Warn(_acct.AmazonAccountId, $"Retrying {requestUrl} in 2 mins Calling MWS ");

                            Thread.Sleep(120000);
                            return getResponse(serviceURL, parms, false);
                        }
                        return rv;
                    }
                    return rv;




                }

            }
            catch (Exception e)
            {
                Log.Error(_acct.AmazonAccountId, "Error in MWS Call", e);
                return "";
            }


        }






    }
}
