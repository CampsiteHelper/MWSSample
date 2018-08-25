
using System;
using System.Xml.Linq;
using Common;
using Persistence;
using ServiceStack.DataAnnotations;

namespace MWSOrders
{
	public class OrderItem : BaseRecordOrmLite<OrderItem>
    {

        public int? QuantityOrdered { get; set; }

        public string Title { get; set; }

        public Double? PromotionDiscount_Amount { get; set; }

        public string ASIN { get; set; }

        public string SellerSKU { get; set; }


        public string OrderItemId { get; set; }

        [PrimaryKey]
        public string OrderOrderItemId { get
            {
                return AmazonOrderId + ":" + OrderItemId;
            }
            set {} }

       

        public int? QuantityShipped { get; set; }

        public Double? ItemPrice_Amount { get; set; }

        public Double? ItemTax_Amount { get; set; }

        [Index]
        public string AmazonOrderId { get; set; }


        [Index]
        public DateTime PurchaseDate { get; set; }

		public int? POItemId { get; set; }

        public OrderItem()
        {
            
        }
		public OrderItem(XElement orderItemElement, string AmazonOrderId,DateTime purchaseDate, AmazonAccount _acct)
        {
            try
            {
				this.AmazonOrderId = AmazonOrderId;
                this.PurchaseDate = purchaseDate;
                this.AmazonAccountId = _acct.AmazonAccountId;
                ASIN = Util.tryGetElementValueString(orderItemElement, "ASIN", true);
                ItemPrice_Amount = Util.tryGetElementValueDouble(orderItemElement.Element("ItemPrice"), "Amount", false);
                ItemTax_Amount = Util.tryGetElementValueDouble(orderItemElement.Element("ItemTax"), "Amount", false);

                OrderItemId = Util.tryGetElementValueString(orderItemElement, "OrderItemId", true);
                PromotionDiscount_Amount = Util.tryGetElementValueDouble(orderItemElement.Element("PromotionDiscount"), "Amount", false);
                QuantityShipped = Util.tryGetElementValueint(orderItemElement, "QuantityShipped", true);
                QuantityOrdered = Util.tryGetElementValueint(orderItemElement, "QuantityOrdered", true);

                SellerSKU = Util.tryGetElementValueString(orderItemElement, "SellerSKU", true);
                Title = Util.tryGetElementValueString(orderItemElement, "Title", true);
                OrderItem.Save(this);


            }
            catch (Exception e)
            {
                Log.Error(_acct.AmazonAccountId,"Error trying to populate orderitem from xelement for orderid " + AmazonOrderId, e);
            }

        }


        }

    }



