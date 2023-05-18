using DataLayer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Logic.Models
{
    public class WorkOrders
    {
        public WorkOrders(IIdentity identity)
        {
            var user = UserHelper.GetUser(identity.Name);
            OrderBacklog = WorkHelper.GetOrderBacklog(user).ToArray();
            WIP = WorkHelper.GetWIP(user).ToArray();
            OrdersShipped = WorkHelper.GetOrdersShipped(user).ToArray();
        }
        public WorkOrderItem[] OrderBacklog { get; }
        public WorkOrderItem[] WIP { get; }
        public WorkOrderItem[] OrdersShipped { get; }
    }
}
