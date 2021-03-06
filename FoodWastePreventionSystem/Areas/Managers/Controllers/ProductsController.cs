﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using FoodWastePreventionSystem.Infrastructure;
using FoodWastePreventionSystem.Models;
using FoodWastePreventionSystem.BusinessLogic;
using FoodWastePreventionSystem.Areas.Managers.Models;
using System.IO;
using System.Diagnostics;
using PagedList;
using System.Data.Entity.Validation;

namespace FoodWastePreventionSystem.Areas.Managers.Controllers
{
    [Authorize(Roles = "admin")]
    [RouteArea("Managers")]
    [RoutePrefix("Products")]
    public class ProductsController : Controller
    {
        private ApplicationContext db = new ApplicationContext();
        private IRepository<Product> ProductRepo;
        private IRepository<Auction> Auctionrepo;
        private IRepository<ProductToBeAuctioned> ToBeAuctionedRepo;
        private ApplicationUserManager userManager;
        private BackgroundTasks Cron { get; set; }


        private SalesLogic SalesLogic;
        private ProductsLogic ProductLogic;
        private AuctionLogic AuctionLogic;
        private int PageSize = 10;

        public ApplicationUser LoggedInUser
        {
            get
            {
                return UserManager.FindById(User.Identity.GetUserId());
            }
        }



        public ApplicationUserManager UserManager
        {
            get
            {
                return userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                userManager = value;
            }
        }


        public ProductsController(IRepository<Product> _ProductRepo, IRepository<FoodWastePreventionSystem.Models.Batch> _ProductInStoreRepo,
                                    IRepository<Transaction> _TransactiontRepo, IRepository<Auction> _AuctionRepo,
                                    IRepository<ProductToBeAuctioned> _ProductToBeAuctionedRepo, IRepository<Loss> _LossRepo,
                                    IRepository<AuctionTransactionStatus> _AuctionTransactionStausRepo, 
                                    AuctionLogic _AuctionL, ProductsLogic _ProductL, SalesLogic _SalesL, BackgroundTasks _cron)
        {
            Cron = _cron;
            ProductRepo = _ProductRepo;
            Auctionrepo = _AuctionRepo;
            ToBeAuctionedRepo = _ProductToBeAuctionedRepo;
            ProductLogic = _ProductL;
            AuctionLogic = _AuctionL;
            SalesLogic = _SalesL;
        }

        [Route("Index/{searchTerm}/{page:int}")]
        [Route("Index/{searchTerm}")]
        [Route("Index/{searchCategory}")]
        [Route("Index/{page:int}")]
        [Route("Index")]
        [Route("")]
        public async Task<ActionResult> Index(string searchTerm, string searchCategory, int? page)
        {
           
            //Background Tasks
            try
            {
                Cron.SearchForExpiredproduct();
                Cron.AddProductToAuctionTable();
                Cron.SendInventoryEmails();

            }
            catch (Exception e)
            {
                string error = "";

                //foreach (var item in e.EntityValidationErrors)
                //{
                //    error += item.Entry;

                //}
                return Content(e.Message+"||||"+e.StackTrace);
            }



            var models = new List<Product>();
            if (!string.IsNullOrWhiteSpace(searchTerm) && !string.IsNullOrWhiteSpace(searchCategory))
            {
                models = await ProductRepo.GetAll(x => x.StoreId == LoggedInUser.StoreId).Where(x => x.Category == searchCategory && x.Name.ToLower().Contains(searchTerm.ToLower()) == true).ToListAsync();
            }
            else if (!string.IsNullOrWhiteSpace(searchTerm) && string.IsNullOrWhiteSpace(searchCategory))
            {
                models = await ProductRepo.GetAll(x => x.StoreId == LoggedInUser.StoreId).Where(x => x.Name.ToLower().Contains(searchTerm.ToLower()) == true).ToListAsync();
            }
            else if (string.IsNullOrWhiteSpace(searchTerm) && !string.IsNullOrWhiteSpace(searchCategory))
            {
                models = await ProductRepo.GetAll(x => x.StoreId == LoggedInUser.StoreId).Where(x => x.Category == searchCategory).ToListAsync();
            }
            else
            {
                models = await ProductRepo.GetAll(x => x.StoreId == LoggedInUser.StoreId).ToListAsync();
            }

            if (string.IsNullOrWhiteSpace(searchCategory))
            {
                ViewBag.Catgory = searchCategory;
                ViewBag.SearchTerm = searchTerm;
            }

            int pageNumber = (page ?? 1);
            var result = await FetchCategories();
            ViewBag.Categories = result;

            return View(models.ToPagedList(pageNumber, PageSize));
        }


        // GET: Managers/Products/Details/5
        public async Task<ActionResult> Details(Guid? id)
        {
            ProductViewModel ProductDetail = new ProductViewModel();
            Dictionary<int, Dictionary<Month, double>> TurnoverReport;

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = await db.Products.FindAsync(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            else
            {
                ProductDetail.InStoreRecords = ProductLogic.RetrieveProductInStoreRecordsForProduct(id.Value);
                ProductDetail.ToBeAuctionedRecords = ToBeAuctionedRepo.GetAll(x => x.Batch.ProductId == id.Value);
                ProductDetail.AuctionRecords = Auctionrepo.GetAll(x => x.Batch.ProductId == id.Value);
                ProductDetail.Product = product;
                ProductDetail.Sales = SalesLogic.GetSalesForProduct(id.Value, out TurnoverReport);

            }
            return View(ProductDetail);
        }

        // GET: Managers/Products/Create
        public async Task<ActionResult> Create()
        {
            ViewBag.Categories = await FetchCategories();
            return View();
        }

        // POST: Managers/Products/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(RegisterProductViewModel productVM, List<HttpPostedFileBase> imageFiles)
        {
            if (ModelState.IsValid)
            {
                Product product = productVM.Product;

                if (!string.IsNullOrWhiteSpace(productVM.NewCategory))
                {
                    product.Category = productVM.NewCategory;
                }
                product.StoreId = LoggedInUser.StoreId;
                ProcessImages(ref product, imageFiles);

                ProductRepo.Add(product);
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            ViewBag.Categories = await FetchCategories();
            return View(productVM);
        }

        [HttpGet]
        public async Task<ActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = await ProductRepo.GetAsync(x => x.Id == id);
            if (product == null)
            {
                return HttpNotFound();
            }

            ViewBag.Categories = await FetchCategories();

            return View(new RegisterProductViewModel()
            {
                Product = product,

            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Exclude = "Product.StoreId")] RegisterProductViewModel productVM, List<HttpPostedFileBase> imageFiles)
        {
            if (ModelState.IsValid)
            {
                Product product = productVM.Product;
                Product oldProduct = ProductRepo.Get(x => x.Id == product.Id);

                if (!string.IsNullOrWhiteSpace(productVM.NewCategory))
                {
                    product.Category = productVM.NewCategory;
                }
                ProcessImages(ref product, imageFiles);


                if (product.Image1 == null)
                {
                    product.Image1 = oldProduct.Image1;
                    product.Image2 = oldProduct.Image2;
                    product.extension1 = oldProduct.extension1;
                    product.extension2 = oldProduct.extension2;
                }

                product.StoreId = LoggedInUser.StoreId;
                db.Entry(product).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            ViewBag.Categories = await FetchCategories();
            return View(productVM);
        }

        // GET: Managers/Products/Delete/5
        public async Task<ActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = await db.Products.FindAsync(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return View(product);
        }

        // POST: Managers/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(Guid id)
        {
            Product product = await db.Products.FindAsync(id);
            db.Products.Remove(product);
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<JsonResult> BlacklistProductJSON(string id)
        {
            Guid productId = new Guid(id);
            Product product = ProductRepo.Get(x => x.Id == productId);
            product.Blacklisted = true;
            var result = await ProductRepo.UpdateAsync(product);
            return Json(result, JsonRequestBehavior.AllowGet);

        }

        [HttpGet]
        public async Task<JsonResult> ToggleProductStatus(string id)
        {
            Guid productId = new Guid(id);
            Product product = ProductRepo.Get(x => x.Id == productId);
            product.Blacklisted = !product.Blacklisted;
            var result = await ProductRepo.UpdateAsync(product);
            return Json(new { Blacklisted= result.Blacklisted}, JsonRequestBehavior.AllowGet);

        }

        private void ProcessImages(ref Product product, List<HttpPostedFileBase> imageFiles)
        {
            for (int i = 0; i < imageFiles.Count; ++i)
            {
                if (imageFiles[i] != null && imageFiles[i].ContentLength > 0)
                {
                    int ContentLength = imageFiles[i].ContentLength;
                    switch (i)
                    {
                        case 0:
                            product.Image1 = new byte[ContentLength];
                            imageFiles[i].InputStream.Read(product.Image1, 0, ContentLength);
                            product.extension1 = imageFiles[i].ContentType;
                            break;
                        case 1:
                            product.Image2 = new byte[ContentLength];
                            imageFiles[i].InputStream.Read(product.Image2, 0, ContentLength);
                            product.extension2 = imageFiles[i].ContentType;
                            break;
                    }

                }
            }
        }

        private async Task<IEnumerable<string>> FetchCategories()
        {
            return await ProductRepo.GetAll(x => x.StoreId == LoggedInUser.StoreId).Select(x => x.Category).Distinct().ToListAsync();

        }

        [AllowAnonymous]
        public FileContentResult GetImage1(Guid id)
        {
            Product prod = ProductRepo.Get(p => p.Id == id);
            if (prod != null && prod.Image1!=null)
            {
                 return File(prod.Image1, prod.extension1);
            }
            else
            {
                return null;
            }
        }

        [AllowAnonymous]
        public FileContentResult GetImage2(Guid id)
        {
            Product prod = ProductRepo.Get(p => p.Id == id);
            if (prod != null && prod.Image1 != null)
            {
                return File(prod.Image2, prod.extension2);
            }
            else
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
