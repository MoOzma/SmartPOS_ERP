using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SmartPOS_ERP.TagHelpers;

[HtmlTargetElement("help-tip")]
public sealed class HelpTipTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var text = (await output.GetChildContentAsync()).GetContent().Trim();
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("class", "help-tip");
        output.Attributes.SetAttribute("aria-label", "شرح");
        output.Attributes.SetAttribute("aria-expanded", "false");
        output.Content.SetHtmlContent(
            "<span class=\"help-tip-icon\" aria-hidden=\"true\">?</span>" +
            $"<span class=\"help-tip-pop\" role=\"tooltip\">{text}</span>");
    }
}
