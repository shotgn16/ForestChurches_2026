using Microsoft.AspNetCore.Razor.TagHelpers;
using ForestChurches.Components.Storage;

namespace ForestChurches.Components.Storage
{
    [HtmlTargetElement("img", Attributes = BlobNameAttributeName)]
    public class ImageSasTagHelper : TagHelper
    {
        private const string BlobNameAttributeName = "image-sas";

        private readonly IBlobUrlService _blobUrlService;

        public ImageSasTagHelper(IBlobUrlService blobUrlService)
        {
            _blobUrlService = blobUrlService;
        }

        /// <summary>
        /// The blob name (path inside container) to generate a SAS URL for.
        /// </summary>
        [HtmlAttributeName(BlobNameAttributeName)]
        public string BlobName { get; set; } = string.Empty;

        /// <summary>
        /// How long the generated SAS URL is valid for (minutes). Defaults to 30.
        /// </summary>
        [HtmlAttributeName("expires-minutes")]
        public int ExpiresMinutes { get; set; } = 30;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (string.IsNullOrEmpty(BlobName))
            {
                base.Process(context, output);
                return;
            }

            var sas = _blobUrlService.GetReadSasUrl(BlobName, System.TimeSpan.FromMinutes(ExpiresMinutes));
            output.Attributes.SetAttribute("src", sas);
            base.Process(context, output);
        }
    }
}
