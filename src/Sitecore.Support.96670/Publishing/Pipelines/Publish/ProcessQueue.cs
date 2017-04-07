using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Publishing.Pipelines.PublishItem;

namespace Sitecore.Support.Publishing.Pipelines.Publish
{
    public class ProcessQueue : Sitecore.Publishing.Pipelines.Publish.ProcessQueue
    {
        private List<ID> childrenIDs;
        private List<ID> globalListChildrenIds;

        protected override void ProcessPublishingCandidate(PublishingCandidate entry, PublishContext context)
        {
            List<PublishingCandidate> list;
            List<PublishingCandidate> list2;
            Assert.ArgumentNotNull(entry, "entry");
            Assert.ArgumentNotNull(context, "context");
            this.ProcessPublishingCandidate(entry, context, out list, out list2);
            if (list2.Any<PublishingCandidate>())
            {
                this.ProcessEntries(list2, context);
            }
            if (list.Any<PublishingCandidate>())
            {
                this.ProcessEntries(this.RemoveDuplicateReferrers(list, context), context);
            }
        }



        protected override void ProcessPublishingCandidate(PublishingCandidate entry, PublishContext context, out List<PublishingCandidate> referrers, out List<PublishingCandidate> children)
        {
            Assert.ArgumentNotNull(entry, "entry");
            Assert.ArgumentNotNull(context, "context");
            if (this.childrenIDs == null)
            {
                this.childrenIDs = new List<ID>();
            }
            referrers = new List<PublishingCandidate>();
            children = new List<PublishingCandidate>();
            bool flag = !context.ProcessedItemIds.Contains(entry.ItemId) | ((this.childrenIDs.Contains(entry.ItemId) && entry.PublishOptions.PublishRelatedItems) && context.ProcessedItemIds.Contains(entry.ItemId));
            if (flag)
            {
                HashSet<ID> processedItemIds = context.ProcessedItemIds;
                lock (processedItemIds)
                {
                    flag = context.ProcessedItemIds.Add(entry.ItemId);
                }
            }
            if (flag || entry.PublishOptions.Deep)
            {
                bool flag3 = flag || !entry.PublishOptions.Deep;
                foreach (Language language in context.Languages)
                {
                    entry.PublishOptions.Language = language;
                    PublishItemContext context2 = this.CreateItemContext(entry, context);
                    context2.PublishOptions.Language = language;
                    PublishItemResult result = null;
                    if (flag3)
                    {
                        result = PublishItemPipeline.Run(context2);
                    }
                    if (((result != null) && (context.PublishOptions.Mode == PublishMode.Incremental)) && ((result.Operation == PublishOperation.Skipped) && result.ShouldBeReturnedToPublishQueue))
                    {
                        PublishManager.AddToPublishQueue(context.PublishOptions.SourceDatabase, context2.ItemId, ItemUpdateType.Skipped, DateTime.Now);
                    }
                    if (entry.PublishAction == PublishAction.DeleteTargetItem)
                    {
                        break;
                    }
                    if ((result != null) && !this.SkipReferrers(result, context))
                    {
                        referrers.AddRange(result.ReferredItems);
                    }
                    if (!children.Any<PublishingCandidate>() && ((result == null) || !this.SkipChildren(result, entry, context)))
                    {
                        List<ID> childrenIDs = this.childrenIDs;
                        lock (childrenIDs)
                        {
                            this.childrenIDs.AddRange(from b in entry.ChildEntries select b.ItemId);
                            if (this.globalListChildrenIds == null)
                            {
                                this.globalListChildrenIds = new List<ID>();
                            }
                            this.globalListChildrenIds.AddRange(from b in entry.ChildEntries select b.ItemId);
                        }
                        children.AddRange(entry.ChildEntries);
                    }
                    if (this.globalListChildrenIds != null)
                    {
                        referrers.RemoveAll(b => this.globalListChildrenIds.Contains(b.ItemId));
                    }
                }
            }
        }
}
}
