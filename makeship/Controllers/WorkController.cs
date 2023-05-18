using Logic;
using Logic.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Logic.Models;
using DataLayer.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;

namespace Timbo.Controllers
{
    //TODO: Define SellerOnly
    //[SellerOnly]
    public class WorkController : Controller
    {
        // GET: Work
        public ActionResult Orders()
        {
            return View(new WorkOrdersViewModel());
        }
        [HttpPost]
        public JsonResult GetOrders()
        {
            //TODO: create workOrdersClass that does not expose unnecessary data
            return Json(new WorkOrders(HttpContext.User?.Identity));
        }
        [HttpPost]
        public JsonResult GenerateInstruction(int orderItemId)
        {
            WorkInstruction wi = null;
            using (var ctx = new UpMiddleContext())
            {
                var orderItem = WorkHelper.GetOrderItemById(orderItemId, ctx);
                wi = new WorkInstruction(orderItem, ctx);
            }
            return Json(wi);
        }
        [HttpPost]
        public JsonResult AcceptWork(int orderItemId)
        {
            using (var ctx = new UpMiddleContext())
            {
                var orderItem = ctx.OrderItems.Where(o => o.OrderItemId == orderItemId).FirstOrDefault();
                if (!orderItem.ItemAccepted && (HttpContext.User?.Identity?.Name == orderItem.SellerId || orderItem.SellerId == null))
                {
                    orderItem.ItemAccepted = true;
                    orderItem.SellerId = HttpContext.User.Identity?.Name;
                    ctx.SaveChanges();
                }
            }

            return GetOrders();
        }
    }
}