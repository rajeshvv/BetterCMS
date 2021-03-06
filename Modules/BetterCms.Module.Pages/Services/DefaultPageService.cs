﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using BetterCms.Core.DataAccess;
using BetterCms.Core.DataAccess.DataContext;
using BetterCms.Core.Exceptions.Mvc;
using BetterCms.Core.Exceptions.Service;
using BetterCms.Core.Models;
using BetterCms.Core.Modules.Projections;
using BetterCms.Core.Services;
using BetterCms.Core.Services.Caching;
using BetterCms.Core.Web;
using BetterCms.Module.Pages.Content.Resources;
using BetterCms.Module.Pages.Models;
using BetterCms.Module.Pages.Projections;
using BetterCms.Module.Root.Models;
using BetterCms.Module.Root.Mvc;
using BetterCms.Module.Root.Projections;

using NHibernate.Linq;

using Page = BetterCms.Module.Pages.Models.PageProperties;
using RootPage = BetterCms.Module.Root.Models.Page;

namespace BetterCms.Module.Pages.Services
{
    public class DefaultPageService : IPageAccessor, IPageService
    {
        private readonly ICacheService cacheService;
        private readonly IRepository repository;
        private readonly IRedirectService redirectService;
        private readonly IUnitOfWork unitOfWork;

        private IDictionary<string, IPage> temporaryPageCache;

        public DefaultPageService(ICacheService cacheService, IRepository repository, IRedirectService redirectService, IUnitOfWork unitOfWork)
        {
            this.cacheService = cacheService;
            this.repository = repository;
            this.redirectService = redirectService;
            this.unitOfWork = unitOfWork;
            this.temporaryPageCache = new Dictionary<string, IPage>();
        }
        
        /// <summary>
        /// Gets current page.
        /// </summary>
        /// <returns>Current page object.</returns>
        public IPage GetCurrentPage(HttpContextBase httpContext)
        {
            // TODO: remove it or optimize it.
            var http = new HttpContextTool(httpContext);
            var virtualPath = HttpUtility.UrlDecode(http.GetAbsolutePath());            
            return GetPageByVirtualPath(virtualPath) ?? new Page(); // TODO: do not return empty page, should implemented another logic.
        }

        /// <summary>
        /// Gets current page by given virtual path.
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <returns>
        /// Current page object by given virtual path.
        /// </returns>
        public IPage GetPageByVirtualPath(string virtualPath)
        {
            if (temporaryPageCache.ContainsKey(virtualPath.ToLowerInvariant()))
            {
                return temporaryPageCache[virtualPath.ToLowerInvariant()];
            }

            var page = repository
                .AsQueryable<PageProperties>(p => p.PageUrl == virtualPath)
                .Fetch(p => p.Layout)
                .FirstOrDefault();

            if (page != null)
            {
                temporaryPageCache.Add(virtualPath.ToLowerInvariant(), page);
            }

            return page;
        }
        
        /// <summary>
        /// Gets a page by id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="loadFull">if set to <c>true</c> load full entity with childs.</param>
        /// <returns>
        /// A page object.
        /// </returns>
        public Page GetPageById(Guid id, bool loadFull = false)
        {
            try
            {
                var query = repository.AsQueryable<PageProperties>(x => x.Id == id);
                if (loadFull)
                {
                    query = query.Fetch(x => x.Author).Fetch(x => x.Layout);
                }
                var page = query.First();
                if (loadFull)
                {
                    page.PageTags = repository.AsQueryable<PageTag>(x => x.Page == page).Fetch(x => x.Tag).ToList();
                }

                return page;
            }
            catch (Exception inner)
            {
                throw new PageException(string.Format("Failed to get page by Id: {0}.", id), inner);
            }
        }

       public void ValidatePageUrl(string url, Guid? pageId = null)
        {
            // Valdiate url
            if (!redirectService.ValidateUrl(url))
            {
                var logMessage = string.Format("Invalid page url {0}.", url);
                throw new ValidationException(e => PagesGlobalization.ValidatePageUrlCommand_InvalidUrlPath_Message, logMessage);
            }
            
            // Is Url unique
            var query = repository.AsQueryable<PageProperties>(page => page.PageUrl == url);
            if (pageId.HasValue && pageId != default(Guid))
            {
                query = query.Where(page => page.Id != pageId.Value);
            }

            if (query.Select(page => page.Id).Any())
            {
                var logMessage = string.Format("Page with entered URL {0} already exists.", url);
                throw new ValidationException(e => PagesGlobalization.ValidatePageUrlCommand_UrlAlreadyExists_Message, logMessage);
            }
        }

        /// <summary>
        /// Gets the redirect for given url.
        /// </summary>
        /// <param name="virtualPath">The virtual path.</param>
        /// <returns>
        /// Redirect URL
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public string GetRedirect(string virtualPath)
        {
            return redirectService.GetRedirect(virtualPath);
        }

        /// <summary>
        /// Gets the list of meta data projections.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The list of meta data projections</returns>
        public IList<IPageActionProjection> GetPageMetaData(IPage page)
        {
            var metaData = new List<IPageActionProjection>();

            var rootPage = page as RootPage;
            if (rootPage != null)
            {
                if (!string.IsNullOrWhiteSpace(rootPage.MetaDescription))
                {
                    metaData.Add(new MetaDataProjection("description", rootPage.MetaDescription));
                }
                if (!string.IsNullOrWhiteSpace(rootPage.MetaKeywords))
                {
                    metaData.Add(new MetaDataProjection("keywords", rootPage.MetaKeywords));
                }
                if (!string.IsNullOrWhiteSpace(rootPage.MetaTitle))
                {
                    metaData.Add(new MetaDataProjection("title", rootPage.MetaTitle));
                }
            }

            var pageProperties = page as Page;
            if (pageProperties != null)
            {
                if (pageProperties.UseNoFollow || pageProperties.UseNoIndex)
                {
                    string robotsContent = null;
                    if (pageProperties.UseNoIndex)
                    {
                        robotsContent = "noindex";
                    }
                    if (pageProperties.UseNoFollow)
                    {
                        if (!string.IsNullOrEmpty(robotsContent))
                        {
                            robotsContent += ",";
                        }
                        robotsContent += "nofollow";
                    }
                    metaData.Add(new MetaDataProjection("robots", robotsContent));
                }
                if (pageProperties.UseCanonicalUrl)
                {
                    metaData.Add(new RelationProjection("canonical", page.PageUrl));
                }
            }
            
            return metaData;
        }
    }
}