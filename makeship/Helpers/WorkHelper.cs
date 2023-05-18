using DataLayer.Models;
using Logic.Models;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Logic
{
    public static class WorkHelper
    {
        public static IEnumerable<WorkOrderItem> GetOrderBacklog(User user)
        {
            if (user == null || !user.IsSeller)
                throw new ArgumentNullException("invalid user");

            using (var ctx = new UpMiddleContext())
            {
                return ctx.OrderItems.AsNoTracking().Include(o1 => o1.Product)
                    .Where(o4 => o4.Seller.Email == user.Email && !o4.ItemShipped && o4.Order.PaymentSucceeded && !o4.ItemAccepted).ToList().Select(ot => new WorkOrderItem(ot.OrderItemId, ot.Product.ProductDesc, new List<string[]>() { new string[] { "Total Man Hours:", "click to find out" } }));
            }
        }
        public static IEnumerable<WorkOrderItem> GetWIP(User user)
        {
            if (user == null || !user.IsSeller)
                throw new ArgumentNullException("invalid user");

            using (var ctx = new UpMiddleContext())
            {
                return ctx.OrderItems.AsNoTracking().Include(o1 => o1.Product)                    
                    .Where(o4 => o4.Seller.Email == user.Email && !o4.ItemShipped && o4.Order.PaymentSucceeded && o4.ItemAccepted).ToList().Select(ot => new WorkOrderItem(ot.OrderItemId, ot.Product.ProductDesc, new List<string[]>() { new string[] { "Total Man Hours:", "click to find out" } }));
            }
        }
        public static IEnumerable<WorkOrderItem> GetOrdersShipped(User user)
        {
            if (user == null || !user.IsSeller)
                throw new ArgumentNullException("invalid user");

            using (var ctx = new UpMiddleContext())
            {
                return ctx.OrderItems.AsNoTracking().Include(o1 => o1.Product)                   
                    .Where(o4 => o4.Seller.Email == user.Email && !o4.ItemShipped && o4.ItemShipped).ToList().Select(ot => new WorkOrderItem(ot.OrderItemId, ot.Product.ProductDesc, new List<string[]>() { new string[] { "Total Man Hours:", "click to find out" } }));
            }
        }
        //public static IEnumerable<IComponentListItem> GetComponentList(Product product, int componentId, Dictionary<string, string> customizations, List<Component> components = null)
        //{
        //    if (components == null)
        //        components = new List<Component>() { new Component() { ManHours = product.AssemblyTime, Description = $"{product.ProductDesc} (assembly)", ProductId = product.ProductId, ComponentId = componentId } };
        //    else
        //        components.Add(new Component() { ManHours = product.AssemblyTime, Description = $"{product.ProductDesc} (assembly)", ProductId = product.ProductId, ComponentId = componentId });

        //    product.Components.ToList().ForEach(c =>
        //    {
        //        if (c.ChildProductId.HasValue)
        //            GetComponentList(c.ChildProduct, c.ComponentId, customizations, components);
        //        else
        //            components.Add(c);
        //    });
        //    components.Reverse();
        //    return components.GroupBy(o => o.ComponentId).Select(g =>
        //    {
        //        var component = g.FirstOrDefault();
        //        if (component != null)
        //        {
        //            return new ComponentListItem()
        //            {
        //                ProductId = component.ProductId.Value,
        //                Quantity = g.Count(),
        //                Description = component.ComponentType == "product" ? $"{component.Description} (assembly)" : component.Description,
        //                ManHours = WorkHelper.EstimateManHours(component, customizations) * g.Count()
        //            };
        //        }
        //        else return null;
        //    });
        //}
        public static IEnumerable<IComponentListItem> GetComponentList(Product product, Dictionary<string, string> customizations, UpMiddleContext ctx)
        {
            //var components = new List<Component>() { new Component() { ManHours = product.AssemblyTime, Description = $"{product.ProductDesc} (assembly)", ProductId = product.ProductId, ComponentId = componentId } };
            var components = ProductHelper.GetComponentCalculations(product.ProductId, customizations, ctx);
            components.Reverse();
            return components.GroupBy(o => new { o.ComponentId, o.X, o.Y, o.Z }).Select(g =>
            {
                var component = g.FirstOrDefault();
                if (component != null)
                {
                    return new ComponentListItem()
                    {
                        ProductId = product.ProductId,
                        Quantity = g.Count(),
                        Description = component.ComponentType == "product" ? $"{component.Description} (assembly)" : component.Description,
                        ManHours = EstimateManHours(component, customizations) * g.Count()
                    };
                }
                else return null;
            });
        }
        public static Instruction PrepareInstructions(this Instruction instruction, Dictionary<string, string> customizations, short recursion = 255)
        {
            if (recursion <= 0)
                throw new Exception("SortInstruction recursion limit exceded.");
            if (instruction.InverseParentInstruction.Count > 0)
            {
                //TODO: Add some kind of ording here.  You'll probably need to create a new Instruction object separate from the DataLayer
                //instruction.InverseParentInstruction = instruction.InverseParentInstruction.OrderBy(i => i.Order).ToList();
                (instruction.InverseParentInstruction as List<Instruction>).ForEach(i => i.PrepareInstructions(customizations, recursion--));
            }
            instruction.Value = CalculateInstruction(instruction.Value, customizations);
            return instruction;
        }
        public static OrderItem GetOrderItemById(int orderItemId, UpMiddleContext ctx)
        {
            return ctx.OrderItems.AsNoTracking()
                        .Include(o1 => o1.Product)                       
                        .Include(o2 => o2.OrderCustomizations.Select(c1 => c1.Material))
                        .Include(o3 => o3.OrderCustomizations.Select(c2 => c2.ProductVariable.Materials))
                        .Where(o => o.OrderItemId == orderItemId).FirstOrDefault();
        }
        
        public static float EstimateManHours(IComponent component, Dictionary<string, string> customizations)
        {
            return ProductHelper.Calculate(component.ManHours, customizations);
            //var manHours = product.Components.Sum(c => ProductHelper.Calculate(c.ManHours, customizations));
            //manHours += product.Components.Where(c => c.ComponentType == "product").Sum(p => EstimateManHours(p.ChildProduct, GetComponentCustomizations(p.ComponentProductCustomizations.ToDictionary(cpc => cpc.Name, cpc => cpc.CustomizationValue), customizations)));
            //return manHours;
        }

        private static Dictionary<string, string> GetComponentCustomizations(Dictionary<string, string> functions, Dictionary<string, string> customizations)
        {
            functions.Keys.ToList().ForEach(k =>
            {
                functions[k] = ProductHelper.Calculate(functions[k], customizations).ToString();
            });
            return functions;
        }
        /// <summary>
        /// With a given list of Required materials, add a new material.  If a matching material already exists in the list, see 
        /// </summary>
        /// <param name="materials"></param>
        /// <param name="materialCalculation"></param>
        public static void AddMaterial(this List<RequiredMaterial> materials, MaterialCalculation materialCalculation)
        {
            var requiredMaterial = materials.Where(m => m.MaterialId == materialCalculation.Material.MaterialId).FirstOrDefault();
            if (requiredMaterial != null)
                requiredMaterial.AddMaterial(materialCalculation);
            else
                materials.Add(new RequiredMaterial(materialCalculation));
        }
        public static string CalculateInstruction(string instruction, Dictionary<string, string> customizations)
        {
            if (instruction != null)
            {
                var args = instruction.Split('|');
                return string.Format(args[0], args.Skip(1).Select(a => FormatCalculation(a, customizations)).ToArray());
            }
            else
                return string.Empty;            
        }
        public static string FormatCalculation(string calculation, Dictionary<string, string> customizations)
        {
            if (calculation[0] == '<')
            {
                var calc = calculation.Split('>');
                var format = calc[0].Substring(1);
                var value = ProductHelper.Calculate(calc[1], customizations);
                if (value > 0)
                {
                    switch (format)
                    {
                        case "cm":
                            return (value * 2.54).ToString("N2") + "cm";
                        case "inch":
                            {
                                var b = Math.Floor(value);
                                var c = Math.Round((value-b) * 16);
                                var d = 16;
                                while (c > 0 && c % 2 == 0)
                                {
                                    c /= 2;
                                    d /= 2;
                                }
                                return (b.ToString() + (c > 0 ? $" {c}\u2044{d}\"" : "\"")).Trim();
                            }
                        case "int":
                            return value.ToString("N0");
                        default:
                            return value.ToString("N2");
                    }
                }
                else
                    return "0";
               
            }
            else
                return ProductHelper.Calculate(calculation, customizations).ToString();
        }
    }
}
