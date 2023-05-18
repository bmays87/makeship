using DataLayer.Models;
using Logic.Attributes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net;
using PayPalCheckoutSdk.Orders;
using Logic.Helpers;
using Logic.Models;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System.Security.Principal;

namespace Logic
{
    public static class OrderHelper
    {

        public static void AddItemToCart(string userName, int productId, Dictionary<string, string> customizations, UpMiddleContext ctx)
        {
            string userId = string.Empty;
            var user = UserHelper.GetUser(userName);
            if (user == null)
                //Create a temporary guest user in database.
                userId = UserHelper.CreateTempUser(userName);
            else
                userId = user.Email;

            var cost = ProductHelper.CalculateCost(ProductHelper.GetComponentCalculations(productId, customizations, ctx), ctx);
            //TODO: Determine seller
            var sellerId = "benedict.mays@arrowdevops.com";

            var orderItem = new OrderItem() { CustomerId = userId, IsInCart = true, ItemCost = cost, MaterialCost = cost, ProductId = productId, Quantity = 1, SellerId = sellerId };
            ctx.OrderItems.Add(orderItem);
            foreach (var customization in customizations)
            {
                if (customization.Key.IndexOf("Input_") == 0)
                {
                    ctx.OrderCustomizations.Add(new OrderCustomization() { OrderItemId = orderItem.OrderItemId, ProductId = orderItem.ProductId, Name = customization.Key, CustomizationValue = customization.Value });
                }
                else if (customization.Key.IndexOf("Material_") == 0)
                {
                    ctx.OrderCustomizations.Add(new OrderCustomization() { OrderItemId = orderItem.OrderItemId, ProductId = orderItem.ProductId, Name = customization.Key, MaterialId = Convert.ToInt32(customization.Value) });
                }
            }
            ctx.SaveChanges();

        }
        private static void UpdateAverageCost(int productId, decimal averageCost, UpMiddleContext ctx)
        {
            var product= ctx.Products.Where(p => p.ProductId == productId).FirstOrDefault();
            if (!product.AverageCost.HasValue || product.AverageCost.Value != averageCost)
            {
                product.AverageCost = product.AverageCost.HasValue ? (averageCost + product.AverageCost.Value) / 2 : averageCost;
                ctx.SaveChanges();
            }
        }
        /// <summary>
        /// Creates the order for the supplied user by add in cart items to the order and applying the shippingAddressId and billingAddressId
        /// </summary>
        /// <param name="user"></param>
        /// <param name="shippingAddressId"></param>
        /// <param name="billingAddressId"></param>
        public static void CreateOrder(IIdentity user, string orderId)
        {
            if (user == null)
                throw new ArgumentNullException("user");

            using (var ctx = new UpMiddleContext())
            {

                var orderItems = ctx.OrderItems.Where(o => o.IsInCart == true && o.CustomerId == user.Name).ToList();
                orderItems.ForEach(o => UpdateAverageCost(o.ProductId, o.ItemCost, ctx));
                var purchaseTotal = orderItems.Sum(o => o.ItemCost);

                var result = GetOrderFromPaypal(ctx, user, orderId, purchaseTotal);

                var order = new DataLayer.Models.Order()
                {
                    OrderId = orderId,
                    OrderSource = "PayPal",
                    ShippingAddressId = result.Item1,
                    BillingAddressId = null,
                    CustomerId = user.Name,
                    IsOpen = true,
                    PaymentSucceeded = result.Item3,
                    PurchaseDate = DateTime.Now,
                    PurchaseTotal = purchaseTotal

                };
                ctx.Orders.Add(order);
                ctx.SaveChanges();

                orderItems.ForEach(o =>
                {
                    o.OrderId = order.OrderId;
                    o.OrderSource = order.OrderSource;
                    o.IsInCart = false;
                });
                ctx.SaveChanges();
            }
        }
        private static Tuple<int, int, bool> GetOrderFromPaypal(UpMiddleContext ctx, IIdentity user, string orderId, decimal expectedPayout)
        {
            int shippingAddressId = 0, billingAddressId = 0;

            var result = JsonConvert.DeserializeObject<PayPalCheckoutSdk.Orders.Order>(PayPalHelper.GetOrderDetails(orderId));
            if (result.PurchaseUnits[0]?.ShippingDetail != null)
            {
                shippingAddressId = ProcessAddress(ctx, user, new Address()
                {
                    AddressLine1 = result.PurchaseUnits[0].ShippingDetail.AddressPortable?.AddressLine1,
                    AddressLine2 = result.PurchaseUnits[0].ShippingDetail.AddressPortable?.AddressLine2,
                    Company = result.PurchaseUnits[0].ShippingDetail.AddressPortable?.AddressDetails?.BuildingName,
                    FirstName = result.Payer?.Name?.GivenName,
                    LastName = result.Payer?.Name?.Surname,
                    City = result.PurchaseUnits[0].ShippingDetail.AddressPortable?.AdminArea2,
                    State = result.PurchaseUnits[0].ShippingDetail.AddressPortable?.AdminArea1,
                    Zip = result.PurchaseUnits[0].ShippingDetail.AddressPortable?.PostalCode,
                    UserId = user.Name
                });
            }
            
            if (shippingAddressId == 0) throw new Exception("Shipping Address not defined!");
            //if (billingAddressId == 0) throw new Exception("Billing Address not defined!");
            bool paymentSucceeded = result.Status.ToUpper() == "APPROVED" &&
                Convert.ToDecimal(result.PurchaseUnits[0].AmountWithBreakdown.Value) == expectedPayout &&
                result.PurchaseUnits[0].Payee.Email == ConfigurationManager.AppSettings["PayPalEmail"];
            return new Tuple<int, int, bool>(shippingAddressId, billingAddressId, paymentSucceeded);
        }

        /// <summary>
        /// Creates a new shipping address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private static int ProcessAddress(UpMiddleContext ctx, IIdentity user, Address address)
        {

            if (address.AddressId != 0)
            {
                if (ctx.Addresses.AsNoTracking().Where(a => a.UserId == user.Name && a.AddressId == address.AddressId).Count() == 1)
                {
                    return address.AddressId;
                }
                else
                    throw new Exception("invalid address id");
            }
            else
            {
                var newAddress = new Address()
                {
                    AddressLine1 = address.AddressLine1,
                    AddressLine2 = address.AddressLine2,
                    FirstName = address.FirstName,
                    LastName = address.LastName,
                    City = address.City,
                    State = address.State,
                    Zip = address.Zip,
                    Company = address.Company,
                    Phone = address.Phone,
                    UserId = user.Name
                };
                ctx.Addresses.Add(newAddress);
                ctx.SaveChanges();

                return newAddress.AddressId;
            }

        }

        public static void UpdateOrderItem(string userName, int productId, Dictionary<string, string> customizations, UpMiddleContext ctx)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return;

            var cost = ProductHelper.CalculateCost(ProductHelper.GetComponentCalculations(productId, customizations, ctx), ctx);

            if (!Int32.TryParse(customizations["OrderItemId"], out int orderItemId))
                return;
            //first make sure our user has ownership of this orderitem
            var orderItem = ctx.OrderItems.Where(o => o.OrderItemId == orderItemId && o.CustomerId == userName).FirstOrDefault();
            if (orderItem == null)
                return;
            //TODO: Item cost should be different than material cost
            orderItem.ItemCost = cost;

            orderItem.MaterialCost = cost;
            ctx.OrderCustomizations.RemoveRange(ctx.OrderCustomizations.Where(c => c.OrderItemId == orderItem.OrderItemId));

            foreach (var customization in customizations)
            {
                if (customization.Key.IndexOf("Input_") == 0)
                {
                    ctx.OrderCustomizations.Add(new OrderCustomization() { OrderItemId = orderItem.OrderItemId, ProductId = orderItem.ProductId, Name = customization.Key, CustomizationValue = customization.Value });
                }
                else if (customization.Key.IndexOf("Material_") == 0)
                {
                    ctx.OrderCustomizations.Add(new OrderCustomization() { OrderItemId = orderItem.OrderItemId, ProductId = orderItem.ProductId, Name = customization.Key, MaterialId = Convert.ToInt32(customization.Value) });
                }
            }
            ctx.SaveChanges();

        }
        public static List<OrderItemModel> GetShoppingCartItems(IIdentity user)
        {
            if (user != null)
            {
                using (var ctx = new UpMiddleContext())
                {
                    return ctx.OrderItems.AsNoTracking().Include(o1 => o1.Product)
                        .Where(o4 => o4.Customer.Email == user.Name && o4.IsInCart).AsEnumerable().Select(o => new OrderItemModel(o, ctx)).ToList();
                }
            }
            else
                return new List<OrderItemModel>();
        }
        public static OrderItem GetOrderItemById(string userName, int orderItemId)
        {
            if (!string.IsNullOrWhiteSpace(userName))
            {
                using (var ctx = new UpMiddleContext())
                {
                    return ctx.OrderItems.AsNoTracking()
                        .Include(o1 => o1.Product)
                        .Include(o2 => o2.OrderCustomizations.Select(c1 => c1.Material))
                        .Include(o3 => o3.OrderCustomizations.Select(c2 => c2.ProductVariable.Materials))
                        .Where(o4 => o4.Customer.Email == userName && o4.OrderItemId == orderItemId).FirstOrDefault();
                }
            }
            else
                return new OrderItem();
        }
        public static void RemoveShoppingCartItem(int itemId, string userName)
        {
            if (!string.IsNullOrWhiteSpace(userName))
            {
                using (var ctx = new UpMiddleContext())
                {
                    var item = ctx.OrderItems.Where(i => i.OrderItemId == itemId && i.CustomerId == userName).FirstOrDefault();
                    if (item != null)
                    {
                        ctx.OrderCustomizations.RemoveRange(ctx.OrderCustomizations.Where(c => c.OrderItemId == item.OrderItemId).ToList());
                        ctx.OrderItems.Remove(item);
                        ctx.SaveChanges();
                    }
                }
            }
        }

        public static IAddress GetUserShippingAddress(User user)
        {
            if (user != null && !user.IsTempUser)
            {
                using (var ctx = new UpMiddleContext())
                {
                    return new ShippingAddress(ctx.Addresses.AsNoTracking().Where(a => a.UserId == user.Email).FirstOrDefault());
                }
            }
            return new ShippingAddress() { FirstName = "", LastName = "", Company = "", Address1 = "", Address2 = "", City = "", State = "AL", Country = "United States", Zip = "" };
        }
    }
}
