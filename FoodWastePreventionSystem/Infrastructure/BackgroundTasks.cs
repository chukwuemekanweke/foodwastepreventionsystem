﻿using FoodWastePreventionSystem.BusinessLogic;
using FoodWastePreventionSystem.Models;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Web;

namespace FoodWastePreventionSystem.Infrastructure
{
    public class BackgroundTasks
    {

        //public IRepository<Product> ProductRepo { get; set; }
        private ProductsLogic ProductLogic {get;set;}

        public BackgroundTasks( ProductsLogic _ProductL)
        {
            ProductLogic = _ProductL;
        }

        public void SearchForExpiredproduct(Guid storeId)
        {
            try
            {
                RecurringJob.AddOrUpdate("search-for-auction-products", ()=> ProductLogic.ProductsPast75PercentOfShelfLife(), Cron.Minutely);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        //public void AddProductBackground(Guid storeId)
        //{
        //    try {
        //        RecurringJob.AddOrUpdate("process-notifications", () => ProductRepo.Add(new Product()
        //        {
        //            Name = "Cron Product",
        //            Description = "crooooooon",
        //            Category = "CRON",
        //            Blacklisted = false,
        //            QuantityPerCarton = 60,
        //            DateProductWasRegistered = DateTime.Now,
        //            BulkPurchaseDiscountPercent = 6,
        //            StoreId = storeId,
                    
        //        }), Cron.Minutely);
        //    }
        //    catch (DbEntityValidationException e)
        //    {
        //        throw e;
        //    }
        //}
    }
}