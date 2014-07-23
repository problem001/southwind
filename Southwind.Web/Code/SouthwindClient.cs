﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using Signum.Web;
using Signum.Utilities;
using Southwind.Entities;
using System.Web.Mvc;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Signum.Entities;
using Signum.Entities.Authorization;
using Signum.Engine;
using Signum.Entities.Basics;
using Signum.Entities.SMS;
using Signum.Entities.Mailing;

namespace Southwind.Web
{
    public static class SouthwindClient
    {
        public static string ViewPrefix = "~/Views/Southwind/{0}.cshtml";
        public static string ThemeSessionKey = "swCurrentTheme";

        public static JsModule OrderModule = new JsModule("Order");

        public static void Start()
        {
            if (Navigator.Manager.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                Navigator.AddSettings(new List<EntitySettings>
                {
                    new EmbeddedEntitySettings<AddressDN>() { PartialViewName = e => ViewPrefix.Formato("Address") },

                    new EntitySettings<TerritoryDN>() { PartialViewName = e => ViewPrefix.Formato("Territory") },
                    new EntitySettings<RegionDN>() { PartialViewName = e => ViewPrefix.Formato("Region") },
                    new EntitySettings<EmployeeDN>() { PartialViewName = e => ViewPrefix.Formato("Employee") },

                    new EntitySettings<SupplierDN>() { PartialViewName = e => ViewPrefix.Formato("Supplier") },
                    new EntitySettings<ProductDN>() { PartialViewName = e => ViewPrefix.Formato("Product") },
                    new EntitySettings<CategoryDN>() { PartialViewName = e => ViewPrefix.Formato("Category") },

                    new EntitySettings<PersonDN>() { PartialViewName = e => ViewPrefix.Formato("Person") },
                    new EntitySettings<CompanyDN>() { PartialViewName = e => ViewPrefix.Formato("Company") },
                   
                    new EntitySettings<OrderDN>() { PartialViewName = e => ViewPrefix.Formato("Order") },
                    new EmbeddedEntitySettings<OrderDetailsDN> { PartialViewName = e => ViewPrefix.Formato("OrderDetails") },
                    new EntitySettings<ShipperDN>() { PartialViewName = e => ViewPrefix.Formato("Shipper") },
                    new EntitySettings<ApplicationConfigurationDN>() { PartialViewName = e => ViewPrefix.Formato("ApplicationConfiguration") },
                });

                Constructor.Register(ctx => new ApplicationConfigurationDN { Sms = new SMSConfigurationDN(), Email = new EmailConfigurationDN() });

                QuerySettings.RegisterPropertyFormat((EmployeeDN e) => e.Photo, new CellFormatter((html, obj) =>
                    obj == null ? null :
                    new MvcHtmlString("<img src=\"data:image/jpg;base64," + Base64Thumbnail((byte[])obj, new Size(48, 48)) + "\" />")));

                QuerySettings.RegisterPropertyFormat((CategoryDN e) => e.Picture, new CellFormatter((html, obj) =>
                    obj == null ? null :
                    new MvcHtmlString("<img src=\"data:image/jpg;base64," + Base64Thumbnail((byte[])obj, new Size(48, 48)) + "\" />")));

                Constructor.Register(ctx => new EmployeeDN { Address = new AddressDN() });
                Constructor.Register(ctx => new OrderDN
                { 
                    ShipAddress = new AddressDN(),
                    Details = new MList<OrderDetailsDN>()
                
                });
                Constructor.Register(ctx => new PersonDN { Address = new AddressDN() });
                Constructor.Register(ctx => new CompanyDN { Address = new AddressDN() });
                Constructor.Register(ctx => new SupplierDN { Address = new AddressDN() });

                Navigator.EntitySettings<EmployeeDN>().MappingMain.AsEntityMapping().RemoveProperty(a => a.Photo);
                Navigator.EntitySettings<CategoryDN>().MappingMain.AsEntityMapping().RemoveProperty(a => a.Picture);

                RegisterQuickLinks();
            }       
        }

        private static void RegisterQuickLinks()
        {
            LinksClient.RegisterEntityLinks<UserDN>((entity, ctx) => new[]
                {
                    new QuickLinkFind(typeof(OperationLogDN), "User", entity, true)
                });

            LinksClient.RegisterEntityLinks<EmployeeDN>((entity, ctx) =>
            {
                var links = new List<QuickLink>()
                {
                    new QuickLinkFind(typeof(OrderDN), "Employee", entity, true)  
                };

                var user = Database.Query<UserDN>().Where(u => entity.RefersTo(u.Related)).Select(u => u.ToLite()).FirstOrDefault();
                if (user != null)
                    links.Add(new QuickLinkView(user));

                return links.ToArray();
            });

            LinksClient.RegisterEntityLinks<CategoryDN>((entity, ctx) =>new []
            {
                new QuickLinkFind(typeof(ProductDN), "Category", entity, true)
            });

            LinksClient.RegisterEntityLinks<SupplierDN>((entity, ctx) => new[]
            {
                new QuickLinkFind(typeof(ProductDN), "Supplier", entity, true)
            });

            LinksClient.RegisterEntityLinks<PersonDN>((entity, ctx) => new[]
            {
                new QuickLinkFind(typeof(OrderDN), "Customer", entity, true)
            });

            LinksClient.RegisterEntityLinks<CompanyDN>((entity, ctx) => new[]
            {
                new QuickLinkFind(typeof(OrderDN), "Customer", entity, true)
            });
        }

        public static string Base64Thumbnail(byte[] image, Size size)
        {
            using(MemoryStream ms = new MemoryStream(image))
            using(Bitmap bmp = new Bitmap(ms))
            using(Bitmap target =  Resize(bmp, size))
            {
                return Convert.ToBase64String(target.SaveJPG100()); 
            }
        }

        public static Bitmap Resize(Bitmap bmpOriginal, Size limit)
        {
            if (bmpOriginal.Size == limit)
            {
                return bmpOriginal;
            }

            Size size = Resize(bmpOriginal.Size, limit);

            Bitmap bmpResized = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(bmpResized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                g.DrawImage(bmpOriginal,
                    new Rectangle(Point.Empty, bmpResized.Size),
                    new Rectangle(Point.Empty, bmpOriginal.Size),
                    GraphicsUnit.Pixel);

                return bmpResized;
            }
        }


        internal static Size Resize(Size original, Size limit)
        {
            if (original.Height < limit.Height && original.Width < limit.Width)
                return original;

            Size r = new Size(limit.Width, original.Height * limit.Width / original.Width);
            if (r.Height <= limit.Height) // Height resize if necessary
                return r;
            return new Size(limit.Height * original.Width / original.Height, limit.Height);
        }

        public static byte[] SaveJPG100(this Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                EncoderParameters encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
                bmp.Save(ms, ImageCodecInfo.GetImageDecoders().First(a=>a.FormatID == ImageFormat.Jpeg.Guid), encoderParameters);
                return ms.ToArray();
            }
        }
    }
}