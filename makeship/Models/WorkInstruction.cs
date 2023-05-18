using DataLayer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic.Models
{
    public class WorkInstruction
    {
        public WorkInstruction(OrderItem orderItem, UpMiddleContext ctx)
        {
            var customizations = orderItem.OrderCustomizations.ToDictionary(k => k.Name, v => v.CustomizationValue);
            RequiredMaterials = null; //TODO: Add method for retrieve required materials
            TotalCost = RequiredMaterials.Sum(r => r.Cost);
            ProductList = WorkHelper.GetComponentList(orderItem.Product, customizations, ctx).ToList();
            TotalManHours = ProductList.Sum(p => p.ManHours);
            Instructions = null; //TODO: Add method for Instruction assignment
        }
        public List<RequiredMaterial> RequiredMaterials { get; }
        public decimal TotalCost { get; }
        public List<IComponentListItem> ProductList { get; }
        public float TotalManHours { get; }
        public List<Instruction> Instructions { get; }
    }   

}
