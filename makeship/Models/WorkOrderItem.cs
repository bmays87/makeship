using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic.Models
{
    public class WorkOrderItem
    {
        public WorkOrderItem(int orderItemId, string productDescription, List<string[]> details )
        {
            OrderItemId = orderItemId;
            ProductDescription = productDescription;
            Details = details;
        }
        public int OrderItemId { get; set; }
        public string ProductDescription { get; set; }
        public List<string[]> Details { get; set; }

    }
}
