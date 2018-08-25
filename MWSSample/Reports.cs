using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using System.Xml.Linq;
  using Common;
  using Persistence;
using ServiceStack.Redis;

  
namespace MWSCommon
{
	public class Report
	{

		AmazonAccount _acct;

		public Report(AmazonAccount acct)
		{
			_acct = acct;

		}


		private string getPriorReportId(string ReportType, IEnumerable<XElement> ReportRequestInfos)
		{

			foreach (var ReportRequestInfo in ReportRequestInfos)
			{
				

				string rType = (string)ReportRequestInfo.Element("ReportType");

				if (!String.IsNullOrEmpty(rType) && rType == ReportType)
				{
					var ReportProcessingStatus = (string)ReportRequestInfo.Element("ReportProcessingStatus");

					//Log.Error(_acct.AmazonAccountId,$"Report  request {ReportRequestId} status = {ReportProcessingStatus}");
					if (ReportProcessingStatus.StartsWith("_DONE_"))
					{
						return (string)ReportRequestInfo.Element("GeneratedReportId");
					}

				}

			}
			return "";
		}



		
		private string getCompletedReportId(string ReportRequestId, string ReportType, bool getPriorRun)
		{

            var responseKey = $"GetReportRequestList:{_acct.AmazonAccountId}";
            string s = "";

            //check cache first
            using(var r = RedisHelper.GetRedisConnection())
            {
                s = r.Get<string>(responseKey);

            }

            if (String.IsNullOrEmpty(s))
            {

                IDictionary<string, string> r1 = new Dictionary<string, String>();

                r1["Action"] = "GetReportRequestList";
                r1["Version"] = "2009-01-01";

                String serviceURL = "https://mws.amazonservices.com";

                r1["MarketplaceId"] = _acct.MarketplaceId;
                r1["SellerId"] = _acct.SellerId;
                r1["ReportTypeList.Type.1"] = ReportType;
                r1["RequestedFromDate"] = AMZNHelper.GetFormattedTimestamp(DateTime.Now.AddDays(-1));

                AMZNWebResponse wr = new AMZNWebResponse(_acct);
                s = wr.getResponse(serviceURL, r1);

                // set cache, expires in 15 seconds
                using (var r = RedisHelper.GetRedisConnection())
                {
                    r.Set<string>(responseKey, s, DateTime.Now.AddSeconds(15));
                    s = r.Get<string>(responseKey);

                }
            }

			try
			{
				var xDoc = XDocument.Parse(s);


				//dynamic root = new ExpandoObject();

				XElement xe = Util.stripNS(xDoc.Elements().First());

				IEnumerable<XElement> ReportRequestInfos = xe.Descendants("ReportRequestInfo");

				if (getPriorRun)
				{
					return getPriorReportId(ReportType, ReportRequestInfos);
				}

				foreach (var ReportRequestInfo in ReportRequestInfos)
				{
					string rId = (string)ReportRequestInfo.Element("ReportRequestId");
					if (!String.IsNullOrEmpty(rId) && rId == ReportRequestId)
					{
						// this will return the last one.
						string ReportProcessingStatus = (string)ReportRequestInfo.Element("ReportProcessingStatus");
                        Log.Info(_acct.AmazonAccountId, $"Report  request {ReportRequestId} status = {ReportProcessingStatus}");
						if (ReportProcessingStatus.StartsWith("_DONE_"))
						{
							return (string)ReportRequestInfo.Element("GeneratedReportId");
						}
						return ReportProcessingStatus;


						// break;

					}
				}
			}
			catch (Exception e)
			{
                Log.Error(_acct.AmazonAccountId, "Error getting report id", e);
				return "";
			}
			return "";



		}

        public void waitForComplete(string ReportRequestId, string ReportType, Dictionary<string, string> parms, int timeoutMS, Func<ReportCache, string> callWhenDone, bool usePrior)
		{
			if (timeoutMS < 10000)
			{
				timeoutMS = 10000;
			}
			string GeneratedReportId = "";

			DateTime startTime = DateTime.Now;
			long genId = 0;
			while (!long.TryParse(GeneratedReportId, out genId) && (DateTime.Now - startTime).TotalMilliseconds < timeoutMS)
			{
                Log.Info(_acct.AmazonAccountId,$"Waiting for report requestid {ReportRequestId}");

				Thread.Sleep(timeoutMS / 10);
				GeneratedReportId = getCompletedReportId(ReportRequestId, ReportType, false);
				if (GeneratedReportId == "_CANCELLED_")
				{
                    Log.Warn(_acct.AmazonAccountId,$"Report request {ReportRequestId} Cancelled");
					GeneratedReportId = "";
					break;
				}
			}



			if (!long.TryParse(GeneratedReportId,out genId) && usePrior)
			{
                Log.Warn(_acct.AmazonAccountId,"Did not get a report back, getting prior version if possible");

				GeneratedReportId = getCompletedReportId(ReportRequestId, ReportType, true);
				if (String.IsNullOrEmpty(GeneratedReportId))
				{
                    Log.Warn(_acct.AmazonAccountId,$"Never got a completed report back for {ReportType} id= {ReportRequestId}");
					return;
				}

			}

			if (long.TryParse(GeneratedReportId, out genId))
			{
                Log.Info(_acct.AmazonAccountId,"Requesting Completed Report " + GeneratedReportId);

				IDictionary<string, string> r1 = new Dictionary<string, String>();

				r1["Action"] = "GetReport";
				r1["SellerId"] = _acct.SellerId;
				r1["MarketplaceId"] = _acct.MarketplaceId;


				r1["ReportId"] = GeneratedReportId;
				r1["Version"] = "2009-01-01";

				String serviceURL = "https://mws.amazonservices.com";

				AMZNWebResponse wr = new AMZNWebResponse(_acct);
				string s = wr.getResponse(serviceURL, r1, false);
                Log.Info(_acct.AmazonAccountId,"Completed Getting Report " + GeneratedReportId + ", calling processing function");

                var rc = ReportCache.fromTSV(ReportType, s, parms, _acct);


				callWhenDone(rc);
			}




		}

        public string RequestReport(string reportName, Dictionary<string, string> r1, Func<ReportCache, string> callWhenDone, int timeoutMS, bool usePrior, bool blockTillComplete)
		{

            Log.Info(_acct.AmazonAccountId,"Requesting Report:" + reportName);


			if (r1 == null)
			{
				r1 = new Dictionary<string, string>();

			}

			r1["Action"] = "RequestReport";
			r1["SellerId"] = _acct.SellerId;

			//r1["ReportId"] = "57572273603";
			r1["Version"] = "2009-01-01";
			r1["MarketplaceIdList.Id.1"] = _acct.MarketplaceId;
			r1["ReportType"] = reportName;

			String serviceURL = "https://mws.amazonservices.com";


			AMZNWebResponse wr = new AMZNWebResponse(_acct);
			string s = wr.getResponse(serviceURL, r1, false);
			//   <ReportRequestId>50016016561</ReportRequestId>
			if (String.IsNullOrEmpty(s))
			{
                Log.Warn(_acct.AmazonAccountId,"Nothing returned from RequestReport");
				return "";
			}
			var xDoc = XDocument.Parse(s);


		
			XElement xe = Util.stripNS(xDoc.Elements().First());

            //	XmlToDynamic.Parse(root, xe); //xDoc.Elements().First());
           

            string ReportRequestID = xe.Element("RequestReportResult").Element("ReportRequestInfo").Element("ReportRequestId").Value;


            Log.Info(_acct.AmazonAccountId,"Report Requested " + reportName + " : " + ReportRequestID);


			var t = Task.Factory.StartNew(() =>
				{
                    //Min wait is one minute
                    System.Threading.Thread.Sleep(60 * 1000);
                   

                    waitForComplete(ReportRequestID, reportName,r1, timeoutMS, callWhenDone, usePrior);

				}
			);
			if (blockTillComplete)
			{
				t.Wait();
			}


			return ReportRequestID;

		}



	}

}

