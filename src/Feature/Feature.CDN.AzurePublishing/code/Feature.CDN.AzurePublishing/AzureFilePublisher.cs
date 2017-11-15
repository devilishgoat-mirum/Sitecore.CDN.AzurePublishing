// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AzureFilePublisher.cs" company="">
//   
// </copyright>
// <summary>
//   The cdn publish.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Sitecore.Feature.CDN.AzurePublishing
{
    using System;
    using System.IO;

    using log4net;

    using Sitecore.Abstractions;
    using Sitecore.Configuration;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Jobs;
    using Sitecore.Publishing;
    using Sitecore.Publishing.Pipelines.PublishItem;

    /// <summary>
    /// The cdn publish.
    /// </summary>
    public class AzureFilePublisher : PublishItemProcessor
    {
        private readonly BaseSettings settings;

        public AzureFilePublisher(BaseSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILog logger = LoggerFactory.GetLogger("Sitecore.Diagnostics.cdnUploading");

        /// <summary>
        /// Gets or sets the enabled.
        /// </summary>
        public string Enabled { get; set; }

        /// <summary>
        /// The process.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        public override void Process(PublishItemContext context)
        {
            this.logger.Info("AzureFilePublisher started the job");

            // Check the configuration to run the processor or not
            if (this.Enabled.ToLower() != "yes")
            {
                return;
            }

            Assert.ArgumentNotNull(context, "context");

            // Get the Context Item
            Item sourceItem = context.PublishHelper.GetSourceItem(context.ItemId);

            // If the source item is null, get the target item (specifically used for deleted item)
            if (sourceItem == null || !sourceItem.Paths.IsMediaItem)
            {
                Item webSourceItem = context.PublishHelper.GetTargetItem(context.ItemId);
                if (webSourceItem == null || !webSourceItem.Paths.IsMediaItem)
                {
                    return;
                }

                sourceItem = webSourceItem;
            }

            MediaItem mediaItem = sourceItem;
            string mediaExtension = mediaItem.Extension;

            Stream mediaStream = mediaItem.GetMediaStream();
            if (mediaStream == null || mediaStream.Length == 0L)
            {
                return;
            }

            AzureStorageUpload azureStorageUpload = new AzureStorageUpload(this.settings);
            try
            {
                // Get Version Information
                Item versionToPublish = context.VersionToPublish;
                if (versionToPublish == null)
                {
                    if (context.PublishHelper.GetTargetItemInLanguage(context.ItemId, sourceItem.Language) != null)
                    {
                        versionToPublish = context.PublishHelper.GetTargetItemInLanguage(
                            context.ItemId, 
                            sourceItem.Language);
                    }
                }

                if (versionToPublish != null)
                {
                    // Parameters to upload/replace/delete from on Azure
                    object[] args = { mediaItem, mediaExtension, versionToPublish.Language.Name };
                    JobOptions jobOptions;
                    Context.Job.Status.State = JobState.Initializing;
                    if (context.Action == PublishAction.None)
                    {
                        jobOptions = new JobOptions(
                            mediaItem.ID.ToString(), 

                            // identifies the job
                            "CDN Upload", 

                            // categoriezes jobs
                            Context.Site.Name, 

                            // context site for job
                            azureStorageUpload, 

                            // object containing method
                            "uploadMediaToAzure", 

                            // method to invoke
                            args) {
                                        // arguments to method
                                                AfterLife = TimeSpan.FromSeconds(5), // keep job data for one hour
                                                EnableSecurity = false // run without a security context
                                            };
                        Context.Job.Status.State = JobState.Finished;
                        JobManager.Start(jobOptions);
                    }

                    if (context.Action == PublishAction.PublishSharedFields
                        || context.Action == PublishAction.PublishVersion)
                    {
                        jobOptions = new JobOptions(
                            mediaItem.ID.ToString(), 
                            "CDN Upload", 
                            Context.Site.Name, 
                            azureStorageUpload, 
                            "replaceMediaFromAzure", 
                            args) {
                                        AfterLife = TimeSpan.FromSeconds(5), EnableSecurity = false 
                                    };
                        Context.Job.Status.State = JobState.Finished;
                        JobManager.Start(jobOptions);
                    }

                    // If the publish action is delete target item, get all the language versions of the item and delete it from Azure
                    if (context.Action == PublishAction.DeleteTargetItem)
                    {
                        foreach (Language lang in context.PublishOptions.TargetDatabase.GetLanguages())
                        {
                            mediaItem = context.PublishHelper.GetTargetItemInLanguage(mediaItem.ID, lang);
                            args = new object[] { mediaItem, mediaItem.Extension, lang.Name };
                            jobOptions = new JobOptions(
                                mediaItem.ID.ToString(), 
                                "CDN Upload", 
                                Context.Site.Name, 
                                azureStorageUpload, 
                                "deleteMediaFromAzure", 
                                args) {
                                            AfterLife = TimeSpan.FromSeconds(5), EnableSecurity = false 
                                        };
                            Context.Job.Status.State = JobState.Finished;
                            JobManager.Start(jobOptions);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Exception exception =
                    new Exception(
                        string.Format(
                            "CDN Processing failed for {1} ({0} version: {2}). {3}", 
                            (object)sourceItem.ID, 
                            (object)sourceItem.Name, 
                            (object)context.VersionToPublish.Language.Name, 
                            (object)ex.Message));
                Log.Error(exception.Message, exception, this);
                this.logger.Error(exception.Message, exception);
                Log.Error(exception.Message, exception, this);
                context.Job.Status.Failed = true;
                context.Job.Status.Messages.Add(exception.Message);
            }

            Log.Info(" CDN synchronization finished ", this);            
        }
    }
}