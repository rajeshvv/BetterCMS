﻿using System.Linq;

using BetterCms.Core.Mvc;
using BetterCms.Core.Mvc.Commands;
using BetterCms.Module.Pages.Models;
using BetterCms.Module.Pages.ViewModels.Category;
using BetterCms.Module.Root.Models;
using BetterCms.Module.Root.Mvc;
using BetterCms.Module.Root.Mvc.Grids.Extensions;
using BetterCms.Module.Root.Mvc.Grids.GridOptions;
using BetterCms.Module.Root.ViewModels.SiteSettings;

using NHibernate.Criterion;
using NHibernate.Transform;

namespace BetterCms.Module.Pages.Commands.GetCategoryList
{
    /// <summary>
    /// A command to get category list by filter.
    /// </summary>
    public class GetCategoryListCommand : CommandBase, ICommand<SearchableGridOptions, SearchableGridViewModel<CategoryItemViewModel>>
    {
        /// <summary>
        /// Executes this command.
        /// </summary>
        /// <param name="request">A filter to search for specific categories.</param>
        /// <returns>A list of categories.</returns>
        public SearchableGridViewModel<CategoryItemViewModel> Execute(SearchableGridOptions request)
        {
            SearchableGridViewModel<CategoryItemViewModel> model;

            request.SetDefaultSortingOptions("Name");

            Category alias = null;
            CategoryItemViewModel modelAlias = null;

            var query = UnitOfWork.Session
                .QueryOver(() => alias)
                .Where(() => !alias.IsDeleted);

            if (!string.IsNullOrWhiteSpace(request.SearchQuery))
            {
                var searchQuery = string.Format("%{0}%", request.SearchQuery);
                query = query.Where(Restrictions.InsensitiveLike(NHibernate.Criterion.Projections.Property(() => alias.Name), searchQuery));
            }

            query = query
                .SelectList(select => select
                    .Select(() => alias.Id).WithAlias(() => modelAlias.Id)
                    .Select(() => alias.Name).WithAlias(() => modelAlias.Name)
                    .Select(() => alias.Version).WithAlias(() => modelAlias.Version))
                .TransformUsing(Transformers.AliasToBean<CategoryItemViewModel>());

            var count = query.ToRowCountFutureValue();

            var categories = query.AddSortingAndPaging(request).Future<CategoryItemViewModel>();

            model = new SearchableGridViewModel<CategoryItemViewModel>(categories.ToList(), request, count.Value);

            return model;
        }
    }
}