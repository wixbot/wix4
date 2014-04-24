//-------------------------------------------------------------------------------------------------
// <copyright file="BalCompiler.cs" company="Outercurve Foundation">
//   Copyright (c) 2004, Outercurve Foundation.
//   This software is released under Microsoft Reciprocal License (MS-RL).
//   The license and further copyright text can be found in the file
//   LICENSE.TXT at the root directory of the distribution.
// </copyright>
// 
// <summary>
// The compiler for the WiX Toolset Bal Extension.
// </summary>
//-------------------------------------------------------------------------------------------------

namespace WixToolset.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;
    using WixToolset.Data;
    using WixToolset.Data.Rows;
    using WixToolset.Extensibility;

    /// <summary>
    /// The compiler for the WiX Toolset Bal Extension.
    /// </summary>
    public sealed class BalCompiler : CompilerExtension
    {
        private SourceLineNumber addedConditionLineNumber;

        /// <summary>
        /// Instantiate a new BalCompiler.
        /// </summary>
        public BalCompiler()
        {
            this.addedConditionLineNumber = null;
            this.Namespace = "http://wixtoolset.org/schemas/v4/wxs/bal";
        }

        /// <summary>
        /// Processes an element for the Compiler.
        /// </summary>
        /// <param name="parentElement">Parent element of element to process.</param>
        /// <param name="element">Element to process.</param>
        /// <param name="contextValues">Extra information about the context in which this element is being parsed.</param>
        public override void ParseElement(XElement parentElement, XElement element, IDictionary<string, string> context)
        {
            switch (parentElement.Name.LocalName)
            {
                case "Bundle":
                case "Fragment":
                    switch (element.Name.LocalName)
                    {
                        case "Condition":
                            this.ParseConditionElement(element);
                            break;
                        default:
                            this.Core.UnexpectedElement(parentElement, element);
                            break;
                    }
                    break;
                case "BootstrapperApplicationRef":
                    switch (element.Name.LocalName)
                    {
                        case "WixStandardBootstrapperApplication":
                            this.ParseWixStandardBootstrapperApplicationElement(element);
                            break;
                        case "WixManagedBootstrapperApplicationHost":
                            this.ParseWixManagedBootstrapperApplicationHostElement(element);
                            break;
                        default:
                            this.Core.UnexpectedElement(parentElement, element);
                            break;
                    }
                    break;
                default:
                    this.Core.UnexpectedElement(parentElement, element);
                    break;
            }
        }

        /// <summary>
        /// Processes an attribute for the Compiler.
        /// </summary>
        /// <param name="sourceLineNumbers">Source line number for the parent element.</param>
        /// <param name="parentElement">Parent element of element to process.</param>
        /// <param name="attribute">Attribute to process.</param>
        /// <param name="context">Extra information about the context in which this element is being parsed.</param>
        public override void ParseAttribute(XElement parentElement, XAttribute attribute, IDictionary<string, string> context)
        {
            SourceLineNumber sourceLineNumbers = Preprocessor.GetSourceLineNumbers(parentElement);

            switch (parentElement.Name.LocalName)
            {
                case "Variable":
                    // at the time the extension attribute is parsed, the compiler might not yet have
                    // parsed the Name attribute, so we need to get it directly from the parent element.
                    XAttribute variableName = parentElement.Attribute("Name");
                    if (null == variableName)
                    {
                        this.Core.OnMessage(WixErrors.ExpectedParentWithAttribute(sourceLineNumbers, "Variable", "Overridable", "Name"));
                    }
                    else
                    {
                        switch (attribute.Name.LocalName)
                        {
                            case "Overridable":
                                if (YesNoType.Yes == this.Core.GetAttributeYesNoValue(sourceLineNumbers, attribute))
                                {
                                    Row row = this.Core.CreateRow(sourceLineNumbers, "WixStdbaOverridableVariable");
                                    row[0] = variableName;
                                }
                                break;
                            default:
                                this.Core.UnexpectedAttribute(parentElement, attribute);
                                break;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Parses a Condition element for Bundles.
        /// </summary>
        /// <param name="node">The element to parse.</param>
        private void ParseConditionElement(XElement node)
        {
            SourceLineNumber sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string condition = this.Core.GetConditionInnerText(node); // condition is the inner text of the element.
            string message = null;

            foreach (XAttribute attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || this.Namespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                        case "Message":
                            message = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        default:
                            this.Core.UnexpectedAttribute(node, attrib);
                            break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            this.Core.ParseForExtensionElements(node);

            // Error check the values.
            if (String.IsNullOrEmpty(condition))
            {
                this.Core.OnMessage(WixErrors.ConditionExpected(sourceLineNumbers, node.Name.LocalName));
            }

            if (null == message)
            {
                this.Core.OnMessage(WixErrors.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Message"));
            }

            if (!this.Core.EncounteredError)
            {
                Row row = this.Core.CreateRow(sourceLineNumbers, "WixBalCondition");
                row[0] = condition;
                row[1] = message;

                if (null == this.addedConditionLineNumber)
                {
                    this.addedConditionLineNumber = sourceLineNumbers;
                }
            }
        }

        /// <summary>
        /// Parses a WixStandardBootstrapperApplication element for Bundles.
        /// </summary>
        /// <param name="node">The element to parse.</param>
        private void ParseWixStandardBootstrapperApplicationElement(XElement node)
        {
            SourceLineNumber sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string launchTarget = null;
            string licenseFile = null;
            string licenseUrl = null;
            string logoFile = null;
            string logoSideFile = null;
            string themeFile = null;
            string localizationFile = null;
            YesNoType suppressOptionsUI = YesNoType.NotSet;
            YesNoType suppressDowngradeFailure = YesNoType.NotSet;
            YesNoType suppressRepair = YesNoType.NotSet;
            YesNoType showVersion = YesNoType.NotSet;

            foreach (XAttribute attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || this.Namespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                        case "LaunchTarget":
                            launchTarget = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "LicenseFile":
                            licenseFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "LicenseUrl":
                            licenseUrl = this.Core.GetAttributeValue(sourceLineNumbers, attrib, EmptyRule.CanBeEmpty);
                            break;
                        case "LogoFile":
                            logoFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "LogoSideFile":
                            logoSideFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "ThemeFile":
                            themeFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "LocalizationFile":
                            localizationFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "SuppressOptionsUI":
                            suppressOptionsUI = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                            break;
                        case "SuppressDowngradeFailure":
                            suppressDowngradeFailure = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                            break;
                        case "SuppressRepair":
                            suppressRepair = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                            break;
                        case "ShowVersion":
                            showVersion = this.Core.GetAttributeYesNoValue(sourceLineNumbers, attrib);
                            break;
                        default:
                            this.Core.UnexpectedAttribute(node, attrib);
                            break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            this.Core.ParseForExtensionElements(node);

            if (String.IsNullOrEmpty(licenseFile) && null == licenseUrl)
            {
                this.Core.OnMessage(WixErrors.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "LicenseFile", "LicenseUrl", true));
            }

            if (!this.Core.EncounteredError)
            {
                if (!String.IsNullOrEmpty(launchTarget))
                {
                    VariableRow row = (VariableRow)this.Core.CreateRow(sourceLineNumbers, "Variable");
                    row.Id = "LaunchTarget";
                    row.Value = launchTarget;
                    row.Type = "string";
                }

                if (!String.IsNullOrEmpty(licenseFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixStdbaLicenseRtf";
                    wixVariableRow.Value = licenseFile;
                }

                if (null != licenseUrl)
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixStdbaLicenseUrl";
                    wixVariableRow.Value = licenseUrl;
                }

                if (!String.IsNullOrEmpty(logoFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixStdbaLogo";
                    wixVariableRow.Value = logoFile;
                }

                if (!String.IsNullOrEmpty(logoSideFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixStdbaLogoSide";
                    wixVariableRow.Value = logoSideFile;
                }

                if (!String.IsNullOrEmpty(themeFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixStdbaThemeXml";
                    wixVariableRow.Value = themeFile;
                }

                if (!String.IsNullOrEmpty(localizationFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixStdbaThemeWxl";
                    wixVariableRow.Value = localizationFile;
                }

                if (YesNoType.Yes == suppressOptionsUI || YesNoType.Yes == suppressDowngradeFailure || YesNoType.Yes == suppressRepair || YesNoType.Yes == showVersion)
                {
                    Row row = this.Core.CreateRow(sourceLineNumbers, "WixStdbaOptions");
                    if (YesNoType.Yes == suppressOptionsUI)
                    {
                        row[0] = 1;
                    }

                    if (YesNoType.Yes == suppressDowngradeFailure)
                    {
                        row[1] = 1;
                    }

                    if (YesNoType.Yes == suppressRepair)
                    {
                        row[2] = 1;
                    }

                    if (YesNoType.Yes == showVersion)
                    {
                        row[3] = 1;
                    }
                }
            }
        }

        /// <summary>
        /// Parses a WixManagedBootstrapperApplicationHost element for Bundles.
        /// </summary>
        /// <param name="node">The element to parse.</param>
        private void ParseWixManagedBootstrapperApplicationHostElement(XElement node)
        {
            SourceLineNumber sourceLineNumbers = Preprocessor.GetSourceLineNumbers(node);
            string licenseFile = null;
            string licenseUrl = null;
            string logoFile = null;
            string themeFile = null;
            string localizationFile = null;
            string netFxPackageId = null;

            foreach (XAttribute attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || this.Namespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                        case "LicenseFile":
                            licenseFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "LicenseUrl":
                            licenseUrl = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "LogoFile":
                            logoFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "ThemeFile":
                            themeFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "LocalizationFile":
                            localizationFile = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "NetFxPackageId":
                            netFxPackageId = this.Core.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        default:
                            this.Core.UnexpectedAttribute(node, attrib);
                            break;
                    }
                }
                else
                {
                    this.Core.ParseExtensionAttribute(node, attrib);
                }
            }

            this.Core.ParseForExtensionElements(node);

            if (String.IsNullOrEmpty(licenseFile) == String.IsNullOrEmpty(licenseUrl))
            {
                this.Core.OnMessage(WixErrors.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "LicenseFile", "LicenseUrl", true));
            }

            if (!this.Core.EncounteredError)
            {
                if (!String.IsNullOrEmpty(licenseFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixMbaPrereqLicenseRtf";
                    wixVariableRow.Value = licenseFile;
                }

                if (!String.IsNullOrEmpty(licenseUrl))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixMbaPrereqLicenseUrl";
                    wixVariableRow.Value = licenseUrl;
                }

                if (!String.IsNullOrEmpty(logoFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "PreqbaLogo";
                    wixVariableRow.Value = logoFile;
                }

                if (!String.IsNullOrEmpty(themeFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "PreqbaThemeXml";
                    wixVariableRow.Value = themeFile;
                }

                if (!String.IsNullOrEmpty(localizationFile))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "PreqbaThemeWxl";
                    wixVariableRow.Value = localizationFile;
                }

                if (!String.IsNullOrEmpty(netFxPackageId))
                {
                    WixVariableRow wixVariableRow = (WixVariableRow)this.Core.CreateRow(sourceLineNumbers, "WixVariable");
                    wixVariableRow.Id = "WixMbaPrereqPackageId";
                    wixVariableRow.Value = netFxPackageId;
                }
            }
        }
    }
}