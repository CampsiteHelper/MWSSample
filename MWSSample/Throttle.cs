using System;
using System.Collections.Generic;
using Common;
using Persistence;

namespace MWSCommon
{
    public static class Throttle
    {


        private static Dictionary<string, int> throttleList = new Dictionary<string, int>()
        {
            //action and milliseconds of recovery rate
            { "ListOrderItems",2000},
            { "ListOrderItemsByNextToken",2000},
            { "ListOrders",60000},
            { "ListOrdersByNextToken",60000},
            { "GetOrder",60000},
           
            { "GetMatchingProduct",500},
            { "GetMatchingProductForId",200},


            { "GetReportRequestList",45000},

            { "ListFinancialEvents",2000},
            { "ListFinancialEventsByNextToken",2000},
            { "ListFinancialEventGroups",2000},
            { "ListFinancialEventGroupsByNextToken",2000},
           
            { "GetTransportContent",500},
            { "ListInboundShipments",500},
            { "ListInboundShipmentsByNextToken",500},
           
            { "SubmitFeed",120000},
            { "GetFeedSubmissionResult",60000},

            { "GetLowestPricedOffersForASIN",200},


           




        };



        public static void waitThrottle(string MWSAction, AmazonAccount acct)
        {
            var key = $"MWSThrottle:{acct.SellerId}:{acct.MarketplaceId}:{MWSAction}";

            DateTime lastCall;

            CheckHourlyQuota(MWSAction, acct);


            using (var redis = RedisHelper.GetRedisConnection())
            {
                lastCall = redis.Get<DateTime>(key);


                if (throttleList.ContainsKey(MWSAction) && lastCall > DateTime.Now.AddDays(-1))
                {

                    var pace = throttleList[MWSAction];

                    var waitTimeMs = pace - (DateTime.Now - lastCall).TotalMilliseconds;

                    if (waitTimeMs > 0)
                    {
                        System.Threading.Thread.Sleep((int)waitTimeMs + 1);
                    }
                }


                redis.Set<DateTime>(key, DateTime.Now);

            }



        }


        private static void CheckHourlyQuota(string action, AmazonAccount acct)
        {
            // check hourly
            string cacheKey = $"MWSHourlyQuotaHold:{acct.SellerId}:{acct.MarketplaceId}:{action}";
            using (var redis = RedisHelper.GetRedisConnection())
            {
                //set a value expire when quota resets.

                var s = redis.Get<DateTime?>(cacheKey);
                if (s != null && s > DateTime.Now)
                {
                    var msToWait = ((DateTime)s - DateTime.Now).TotalMilliseconds;
                    System.Threading.Thread.Sleep((int)msToWait);
                }

            }


        }

        public static void SetHourlyQuotaHold(string action, Single remaining, DateTime quotaResetWhen, AmazonAccount acct)
        {

            if (remaining < 2.0)
            {

                // see if we need to wait
                string cacheKey = $"MWSHourlyQuotaHold:{acct.SellerId}:{acct.MarketplaceId}:{action}";

                using (var redis = RedisHelper.GetRedisConnection())
                {
                    //set a value expire when quota resets.

                    redis.Set<DateTime>(cacheKey, quotaResetWhen, quotaResetWhen);

                }

            }
        }


    }




}
