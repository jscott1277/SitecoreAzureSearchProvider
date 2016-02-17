1. Follow the instructions in the libs/Readme.txt file  
2. Open solution file found in the Website folder in Visual Studio 2015
3. Update the publish settings of the Website project to publish to your local Sitecore instance (Default location:  C:\inetpub\wwwroot\sc81rev151207\Website)
4. Publish the Website project
5. Copy the Jarstan.Settings.config to the sitecore_web_root\App_Config\Include\Jarstan.ContentSearch folder (One time step)
   NOTE:  If you rename this file, it MUST be renamed so that it appears after the Jarstan.ContentSearch.Azure.DefaultIndexConfiguration.config file alphabetically so that it patches properly.
6. Update the following two settings, based on your Azure Search instance, in the Jarstan.Settings.config file (After you copy it):
   a. AzureSearchServiceName
   b. AzureSearchServiceApiKey
7. Log into the Sitecore Admin website
8. Open the Control Panel applet
9. Click the 'Indexing manager' link
10. Tick the box for the 'azure-sitecore-master-index' index
11. Delete the current version of Newtonsoft.Json.dll from the sitecore_web_root\bin folder of the Sitecore instance. 
12. Click the 'Rebuild' button (NOTE:  The Root property of the Crawler is set to /sitecorey by default, so all applicable items will be indexed.  This value is available in the sitecore_web_roo/App_Config/Include/Jarstan.ContentSearch/Jarstan.ContentSearch.AzureMaster.config file)
13. Browse to the website's SearchResults.aspx page to view test search results. (You can also use the Azure Portal to view indexed data)
