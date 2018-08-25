
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Common;
using Persistence;
using ServiceStack.DataAnnotations;

namespace MWSOrders
{
    [Alias("Orders")]
	public class Order : BaseRecordOrmLite<Order>
    {
        public DateTime? LatestShipDate { get; set; }

        
        public DateTime PurchaseDate { get; set; }

        public DateTime LastUpdateDate { get; set; }

        public string OrderType { get; set; }

        public string BuyerEmail { get; set; }

        [PrimaryKey]
        public string AmazonOrderId { get; set; }

        public string ShipServiceLevel { get; set; }

        public int? NumberOfItemsShipped { get; set; }

        public string OrderStatus { get; set; }

        public string SalesChannel { get; set; }

        public string IsBusinessOrder { get; set; }

        public string BuyerName { get; set; }

        public double OrderTotal { get; set; }

        public DateTime? EarliestShipDate { get; set; }

        public string MarketplaceId { get; set; }

        public string FulfillmentChannel { get; set; }

        public string PaymentMethod { get; set; }

        public string ShippingAddress_AddressLine1 { get; set; }
        public string ShippingAddress_AddressLine2 { get; set; }

        public string ShippingAddress_State { get; set; }

        public string ShippingAddress_City { get; set; }

        public string ShippingAddress_Postal { get; set; }

        public string IsPrime { get; set; }

        public string ShipmentServiceLevelCategory { get; set; }

        public string SellerOrderId { get; set; }

        public List<OrderItem> OrderItems { get; set; }


        public Order()
        {
        }


		public Order(XElement order,AmazonAccount acct)
        {

            AmazonAccountId = acct.AmazonAccountId;

            AmazonOrderId = Util.tryGetElementValueString(order, "AmazonOrderId", true);
            BuyerEmail = Util.tryGetElementValueString(order, "BuyerEmail", false);
            BuyerName = Util.tryGetElementValueString(order, "BuyerName", false);
            EarliestShipDate = Util.tryGetElementValueDate(order, "EarliestShipDate", true);
            FulfillmentChannel = Util.tryGetElementValueString(order, "FulfillmentChannel", true);
            IsBusinessOrder = Util.tryGetElementValueString(order, "IsBusinessOrder", true);
            IsPrime = Util.tryGetElementValueString(order, "IsPrime", true);
            var dt = Util.tryGetElementValueDate(order, "LastUpdateDate", true);
            if (dt != null)
                LastUpdateDate = (DateTime)dt;
            
            logtimestamp= DateTime.Now;

            MarketplaceId = Util.tryGetElementValueString(order, "MarketplaceId", true);
            NumberOfItemsShipped = Util.tryGetElementValueint(order, "NumberOfItemsShipped", true);


            OrderStatus = Util.tryGetElementValueString(order, "OrderStatus", true);
            try{
                OrderTotal = Double.Parse(Util.tryGetElementValueString(order, "OrderTotal", false).Replace("USD", ""));
            }
            catch(Exception){}
            OrderType = Util.tryGetElementValueString(order, "OrderType", true);
            PaymentMethod = Util.tryGetElementValueString(order, "PaymentMethod", false);
            dt = Util.tryGetElementValueDate(order, "PurchaseDate", true);
            if (dt != null)
                PurchaseDate = (DateTime)dt;
            
            SalesChannel = Util.tryGetElementValueString(order, "SalesChannel", true);
            SellerOrderId = Util.tryGetElementValueString(order, "SellerOrderId", false); //not there with MFN
            ShipmentServiceLevelCategory = Util.tryGetElementValueString(order, "ShipmentServiceLevelCategory", true);
            ShippingAddress_City = Util.tryGetElementValueString(order.Element("ShippingAddress"), "City", false);
            ShippingAddress_Postal = Util.tryGetElementValueString(order.Element("ShippingAddress"), "PostalCode", false);
            ShippingAddress_State = Util.tryGetElementValueString(order.Element("ShippingAddress"), "StateOrRegion", false);
            ShippingAddress_AddressLine1 = Util.tryGetElementValueString(order.Element("ShippingAddress"), "AddressLine1", false);
            ShippingAddress_AddressLine2 = Util.tryGetElementValueString(order.Element("ShippingAddress"), "AddressLine2", false);


            ShipServiceLevel = Util.tryGetElementValueString(order, "ShipServiceLevel", true);

            this.logtimestamp = DateTime.Now;
            Order.Save(this);





        }


    }
}