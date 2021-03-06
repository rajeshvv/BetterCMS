using FluentNHibernate.Mapping;

namespace BetterCms.Core.Models
{
    /// <summary>
    /// Fluent nHibernate entity map base class.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to map.</typeparam>
    public abstract class EntityMapBase<TEntity> : ClassMap<TEntity>
        where TEntity : Entity
    {
        private string moduleName;

        protected string SchemaName
        {
            get
            {
                return "bcms_" + moduleName;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBase{TEntity}" /> class.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        protected EntityMapBase(string moduleName)
        {
            this.moduleName = moduleName;

            Schema(SchemaName);

            Id(x => x.Id).GeneratedBy.GuidComb();

            Map(x => x.IsDeleted).Not.Nullable();
            
            Map(x => x.CreatedOn).Not.Nullable().LazyLoad();
            Map(x => x.ModifiedOn).Not.Nullable().LazyLoad();
            Map(x => x.DeletedOn).Nullable().LazyLoad();

            Map(x => x.CreatedByUser).Not.Nullable().Length(MaxLength.Name).LazyLoad();
            Map(x => x.ModifiedByUser).Not.Nullable().Length(MaxLength.Name).LazyLoad();
            Map(x => x.DeletedByUser).Nullable().Length(MaxLength.Name).LazyLoad();
            
            Version(x => x.Version);

            OptimisticLock.Version();
        }
    }
}
