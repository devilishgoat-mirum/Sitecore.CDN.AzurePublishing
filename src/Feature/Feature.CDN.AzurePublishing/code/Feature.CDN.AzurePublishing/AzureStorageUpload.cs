namespace Sitecore.Feature.CDN.AzurePublishing
{
    using log4net;

    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    using Sitecore.Abstractions;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;

    /// <summary>
    /// The azure storage upload.
    /// </summary>
    public class AzureStorageUpload
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILog logger = LoggerFactory.GetLogger("Sitecore.Diagnostics.cdnUploading");

        /// <summary>
        /// The container.
        /// </summary>
        private readonly CloudBlobContainer container;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorageUpload"/> class.
        /// </summary>
        /// <param name="settings">
        /// The settings.
        /// </param>
        public AzureStorageUpload(BaseSettings settings)
        {            
            var containerName = settings.GetSetting("Azure.ContainerName");
            string connectionString = settings.GetSetting("Azure.StorageConnectionString");

            // Use ConfigurationManager to retrieve the connection string
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            // Create a CloudBlobClient object using the storage account to retrieve objects that represent containers and blobs stored within the Blob Storage Service
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.
            this.container = blobClient.GetContainerReference(containerName);

            // Create the container if it doesn't already exist.
            this.container.CreateIfNotExists();

            // By default, the new container is private and you must specify your storage access key to download blobs from this container. If you want to make the files within the container available to everyone, you can set the container to be public
            this.container.SetPermissions(
                new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        /// <summary>
        /// The delete media from azure.
        /// </summary>
        /// <param name="mediaItem">
        /// The media item.
        /// </param>
        /// <param name="extension">
        /// The extension.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        public void DeleteMediaFromAzure(MediaItem mediaItem, string extension = "", string language = "")
        {
            CloudBlockBlob blockBlob = this.container.GetBlockBlobReference(this.GetMediaPath(mediaItem, extension));
            if (blockBlob.DeleteIfExists())
            {
                this.logger.Info(string.Format(" CDN File Deleted : {0}", this.GetMediaPath(mediaItem, extension)));
            }
        }

        /// <summary>
        /// The get media path.
        /// </summary>
        /// <param name="mediaItem">
        /// The media item.
        /// </param>
        /// <param name="extension">
        /// The extension.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string GetMediaPath(MediaItem mediaItem, string extension = "")
        {
            string newFileName = MainUtil.EncodeName(mediaItem.Name);

            return
                mediaItem.MediaPath.TrimStart('/')
                    .Replace(mediaItem.DisplayName, newFileName + "." + extension)
                    .ToLower();
        }

        /// <summary>
        /// The replace media from azure.
        /// </summary>
        /// <param name="mediaItem">
        /// The media item.
        /// </param>
        /// <param name="extension">
        /// The extension.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        public void ReplaceMediaFromAzure(MediaItem mediaItem, string extension = "", string language = "")
        {            
            this.DeleteMediaFromAzure(mediaItem, extension, language);  
                        
            CloudBlockBlob blockBlob = this.container.GetBlockBlobReference(this.GetMediaPath(mediaItem, extension));

            if (string.IsNullOrEmpty(mediaItem.Extension))
            {
                return;
            }

            using (var fileStream = (System.IO.FileStream)mediaItem.GetMediaStream())
            {
                blockBlob.Properties.ContentType = mediaItem.MimeType;
                blockBlob.UploadFromStream(fileStream);
                this.SetCacheControl(blockBlob, "public,max-age=691200");
            }      

            this.logger.Info(string.Format("CDN File Uploaded : {0}", this.GetMediaPath(mediaItem, extension)));
        }

        /// <summary>
        /// The set cache control.
        /// </summary>
        /// <param name="blob">
        /// The blob.
        /// </param>
        /// <param name="value">
        /// The value.
        /// </param>
        public void SetCacheControl(CloudBlob blob, string value)
        {
            blob.Properties.CacheControl = value;
            blob.SetProperties();
        }

        /// <summary>
        /// The upload media to azure.
        /// </summary>
        /// <param name="mediaItem">
        /// The media item.
        /// </param>
        /// <param name="extension">
        /// The extension.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        public void UploadMediaToAzure(MediaItem mediaItem, string extension = "", string language = "")
        {
            CloudBlockBlob blockBlob = this.container.GetBlockBlobReference(this.GetMediaPath(mediaItem, extension));
            blockBlob.DeleteIfExists();

            if (string.IsNullOrEmpty(mediaItem.Extension))
            {
                return;
            }

            if (mediaItem.HasMediaStream("Media"))
            {
                using (var fileStream = (System.IO.FileStream)mediaItem.GetMediaStream())
                {
                    blockBlob.Properties.ContentType = mediaItem.MimeType;
                    blockBlob.UploadFromStream(fileStream);
                    this.SetCacheControl(blockBlob, "public,max-age=691200");
                }
            }
            else
            {
                blockBlob.DeleteIfExists();
            }

            this.logger.Info(string.Format("CDN File Uploaded : {0}", this.GetMediaPath(mediaItem, extension)));
        }
    }
}