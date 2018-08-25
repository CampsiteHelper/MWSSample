using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Xml.Linq;
    using Common;
    using MWSCommon;
    using MWSOrders;
    using Persistence;

namespace MWSOrders
{
	public class OrderDownloader
	{


		private AmazonAccount _acct;
	
		public OrderDownloader(AmazonAccount acct)
		{
			_acct = acct;


		}



		String serviceURL = "https://mws.amazonservices.com/Orders/2013-09-01";


		public void getListOfOrders(IEnumerable<string> AmazonOrderIds)
		{
			
			var orderArray = AmazonOrderIds.ToArray();
			if (orderArray.Length > 50)
			{
                Log.Warn(_acct.AmazonAccountId, "getListOfOrders got more than 50 will truncate to 50");
			}
			else
			{
                Log.Info(_acct.AmazonAccountId, $"getListOfOrders processing {orderArray.Length} orders");
			}
			IDictionary<string, string> r1 = new Dictionary<string, String>();


			r1["Action"] = "GetOrder";
			r1["SellerId"] = _acct.SellerId;
			r1["MarketplaceId.Id.1"] = _acct.MarketplaceId;
			for (var i = 1; i <= orderArray.Length && i <= 50; i++)
			{
				r1[$"AmazonOrderId.Id.{i}"] = orderArray[i-1];
			}


			var startTime = DateTime.Now;

           
			AMZNWebResponse wr = new AMZNWebResponse(_acct);
			string s = wr.getResponse(serviceURL, r1);


			var xDoc = XDocument.Parse(s);


			XElement xe = Util.stripNS(xDoc.Elements().First());

			IEnumerable<XElement> Orders = xe.Descendants("Orders").Descendants("Order");
			if (Orders == null || Orders.Count() == 0)
			{
                Log.Warn(_acct.AmazonAccountId, $"No orders came back");

			}
			else
			{
                Log.Info(_acct.AmazonAccountId, $"Storing  {Orders.Count()} orders back from GetOrder..");
				foreach (XElement OrderElement in Orders)
				{
					Order order = new Order(OrderElement, _acct);
                    order.OrderItems = getOrderItems(order);
                    order.logtimestamp = DateTime.Now;
                    Order.Save(order);

                    }
			}

		



		}
		public void getSingleOrder(string AmazonOrderId)
		{


			List<string> list = new List<string>();
			list.Add(AmazonOrderId);
			getListOfOrders(list);


		}
		
		

        /*
		public void doHistory()
		{
			var bt = BatchTracking.getNextPeriod("OrderBatch", 15, _acct);
			if (bt == null)
			{
				System.Threading.Thread.Sleep(60000);
				return;
			}
			getOrdersFromDateRange(bt.startDate, bt.endDate);
			//this will jump us ahead if we make it to end
			bt.save();


		}
*/
        /*
		public void updatePendingOrders()
		{

			var recs = ShipmentEvent.repo.Query<string>("select distinct amazonorderid from Orders where OrderStatus='Pending' and purchasedate<getdate()-1 and AmazonAccountId = @0", _acct.AmazonAccountId);
			var orderIds = recs.ToList();
			Log.Error(_acct.AmazonAccountId, $"Got {orderIds.Count()} pending orders to update");
			List<string> OrdersToGet = new List<string>();

			foreach (var orderID in orderIds)
			{
				try
				{
					OrdersToGet.Add(orderID);
					if (OrdersToGet.Count() >= 50)
					{
						getListOfOrders(OrdersToGet);
						OrdersToGet = new List<string>();
					}

				}
				catch (Exception e)
				{
					Log.Error(_acct.AmazonAccountId,"getOrdersFromFinancials error", e);
				}

			}

			if (OrdersToGet.Count() > 0)
			{
				getListOfOrders(OrdersToGet);

			}
		}
		*/

        /*
		public void getOrdersFromFinancials()
		{

			var recs = ShipmentEvent.repo.Query<string>("select distinct amazonorderid from shipmentevents  where shipmentevents.AmazonAccountId = @0 and not exists (select * from Orders where orders.amazonorderid = shipmentevents.amazonorderid and orderStatus='Shipped' and Orders.AmazonAccountId=@0) and PostedDate > '2016-01-01'", _acct.AmazonAccountId);
			var orderIds = recs.ToList();
			List<string> OrdersToGet = new List<string>();

			var ttl = orderIds.Count;
			var cnt = 0;

			foreach (var orderID in orderIds)
			{
				cnt++;
				if(cnt%100==0)
					Log.Error(_acct.AmazonAccountId,$"Processed {cnt}/{ttl} missing orders");

				try
				{
					OrdersToGet.Add(orderID);
					if (OrdersToGet.Count() >= 50)
					{
						getListOfOrders(OrdersToGet);
						OrdersToGet = new List<string>();
					}

				}
				catch (Exception e)
				{
					Log.Error(_acct.AmazonAccountId, "getOrdersFromFinancials error", e);
				}

			}

			if (OrdersToGet.Count() > 0)
			{
				getListOfOrders(OrdersToGet);

			}

		}
*/

		public void getOrdersFromDateRange(DateTime startDate, DateTime endDate)
		{


			var startTime = DateTime.Now;

			Log.Info(_acct.AmazonAccountId, $"Preparing to get Order and Details from {startDate} to {endDate} ");

			IDictionary<string, string> r1 = new Dictionary<string, String>();

			r1["Action"] = "ListOrders";
			r1["SellerId"] = _acct.SellerId;

			r1["MarketplaceId.Id.1"] = _acct.MarketplaceId;

			r1["LastUpdatedAfter"] = AMZNHelper.GetFormattedTimestamp(startDate);
			r1["LastUpdatedBefore"] = AMZNHelper.GetFormattedTimestamp(endDate);

			//SecureLocalStore.storeItem("lastOrdersDownload", DateTime.Now.ToString());


			AMZNWebResponse wr = new AMZNWebResponse(_acct);


			string s = wr.getResponse(serviceURL, r1);

			var xDoc = XDocument.Parse(s);


			XElement xe = Util.stripNS(xDoc.Elements().First());

			IEnumerable<XElement> Orders = xe.Descendants("Orders").Descendants("Order");
			//Log.Error(_acct.AmazonAccountId, s);


			persistOrders(Orders);


			if (xe.Element("ListOrdersResult") != null && xe.Element("ListOrdersResult").Element("NextToken") != null)
			{
				GetOrdersNextTokens(xe.Element("ListOrdersResult").Element("NextToken").Value);
			}

			var totalMS = (int)(DateTime.Now - startDate).TotalMilliseconds;
			var minMS = 60 * 1000; //one minute

			if (minMS - totalMS > 0)
			{

				Thread.Sleep(minMS - totalMS);
			}

            Log.Info(_acct.AmazonAccountId, "Completed getting order and order details");






		}


		public void getOrdersBetween(DateTime startDate, DateTime endDate)
		{
            
			
			Log.Info(_acct.AmazonAccountId,$"Preparing to get Order and Details going back from {startDate} to {endDate} ");

			IDictionary<string, string> r1 = new Dictionary<string, String>();

			r1["Action"] = "ListOrders";
			r1["SellerId"] = _acct.SellerId;

			r1["MarketplaceId.Id.1"] = _acct.MarketplaceId;

			r1["CreatedAfter"] = AMZNHelper.GetFormattedTimestamp(startDate);
			r1["CreatedBefore"] = AMZNHelper.GetFormattedTimestamp(endDate);


			AMZNWebResponse wr = new AMZNWebResponse(_acct);


			string s = wr.getResponse(serviceURL, r1);

			var xDoc = XDocument.Parse(s);


			XElement xe = Util.stripNS(xDoc.Elements().First());

			IEnumerable<XElement> Orders = xe.Descendants("Orders").Descendants("Order");
			persistOrders(Orders);


			if (xe.Element("ListOrdersResult") != null && xe.Element("ListOrdersResult").Element("NextToken") != null)
			{
				GetOrdersNextTokens(xe.Element("ListOrdersResult").Element("NextToken").Value);
			}

			Log.Info(_acct.AmazonAccountId,"Completed getting order and order details");








		}

		private void GetOrdersNextTokens(string nextToken)
		{




			var done = false;
			while (!done)
			{

				IDictionary<string, string> r1 = new Dictionary<string, String>();
				r1["Action"] = "ListOrdersByNextToken";
				r1["NextToken"] = nextToken;
				r1["SellerId"] = _acct.SellerId;
				r1["MarketplaceId"] = _acct.MarketplaceId;



				AMZNWebResponse wr = new AMZNWebResponse(_acct);
				string s = wr.getResponse(serviceURL, r1);


				var xDoc = XDocument.Parse(s);


				XElement xe = Util.stripNS(xDoc.Elements().First());

				IEnumerable<XElement> allOrders = xe.Descendants("Orders");
				if (allOrders != null)
				{
					var Orders = allOrders.Descendants("Order");

					persistOrders(Orders);

				}

				if (xe.Element("ListOrdersByNextTokenResult") != null && xe.Element("ListOrdersByNextTokenResult").Element("NextToken") != null)
				{
					nextToken = xe.Element("ListOrdersByNextTokenResult").Element("NextToken").Value;
				}
				else
				{
					done = true;
				}

			}


		}

		private void persistOrders(IEnumerable<XElement> Orders)
		{
			if (Orders != null)
			{
				int cntOrders = Orders.Count();
				Log.Info(_acct.AmazonAccountId,"Got " + cntOrders + " orders, importing to database");

				int cntDone = 0;
				foreach (XElement OrderElement in Orders)
				{
					try
					{
						Order order = new Order(OrderElement, _acct);

                        order.OrderItems = getOrderItems(order);
                        order.logtimestamp = DateTime.Now;
                        Order.Save(order);


						if (cntDone % 5 == 0)
						{
							Log.Info(_acct.AmazonAccountId,$"Processed {cntDone}/{cntOrders} thru {order.PurchaseDate}");


						}


					}
					catch (Exception e)
					{
						Log.Error(_acct.AmazonAccountId,"Error processing order", e);
					}
					cntDone++;
				}
			}

		}




        private List<OrderItem> getOrderItems(Order order)
		{
            var items = new List<OrderItem>();

			try
			{
				AMZNWebResponse orderItemsResp = new AMZNWebResponse(_acct);
				
				IDictionary<string, string> ordItemsParms = new Dictionary<string, String>();

				ordItemsParms["Action"] = "ListOrderItems";

				ordItemsParms["SellerId"] = _acct.SellerId;

				ordItemsParms["AmazonOrderId"] = order.AmazonOrderId;

               
				string s = orderItemsResp.getResponse(serviceURL, ordItemsParms);

				if (!s.Contains("OrderItem"))
					return items;
				
				var xDoc = XDocument.Parse(s);


				XElement xe = Util.stripNS(xDoc.Elements().First());

				IEnumerable<XElement> OrderItems = xe.Descendants("OrderItem");
				foreach (XElement OrderItemElement in OrderItems)
				{
					//will persist itself

                    var oi = new OrderItem(OrderItemElement, order.AmazonOrderId,(DateTime)order.PurchaseDate,_acct);
                    items.Add(oi);

					

				}
				// restore rate is 30 per minute
				//Thread.Sleep(1900);


			}
			catch (Exception e)
			{
				Log.Error(_acct.AmazonAccountId,"Error in getOrderItems", e);
				Log.Error(_acct.AmazonAccountId,e.StackTrace,e);

			}
            return items;


		}
	}

}
