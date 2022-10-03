using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace BBWS_Inventory_Tracking
{
    public class CreateNewInventoryTracking : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);


            if (!context.InputParameters.ContainsKey("Target"))
            {
                throw new InvalidPluginExecutionException("No target found");
            }

            Entity ServiceTaskInput = context.InputParameters["Target"] as Entity;

            if (!ServiceTaskInput.Contains("msdyn_percentcomplete")) { return;}
            if (ServiceTaskInput["msdyn_percentcomplete"].ToString() != "100") { return;}


            string fetchXMLServiceTask = @"<fetch top='1'>" +
                      "<entity name = 'msdyn_workorderservicetask'>" +
                      "<filter>" +
                      "<condition attribute = 'msdyn_workorderservicetaskid' operator= 'eq' value ='" + ServiceTaskInput.Id + "'/>" +
                      "</filter>" +
                      "</entity>" +
                      "</fetch>";

            Entity ServiceTask = service.RetrieveMultiple(new FetchExpression(fetchXMLServiceTask)).Entities[0];
            if (!ServiceTask.Contains("msdyn_tasktype")) { return;}

            if (ServiceTask.Contains("msdyn_workorder"))
            {
                string fetchXMLWorkOrder = @"<fetch top='1'>" +
                    "<entity name = 'msdyn_workorder'>" +
                    "<attribute name='bcs_location'/>" +
                    "<attribute name='bcs_containerserialno'/>" +
                    "<filter>" +
                    "<condition attribute = 'msdyn_workorderid' operator= 'eq' value ='" + ((EntityReference)ServiceTask["msdyn_workorder"]).Id + "'/>" +
                    "</filter>" +
                    "</entity>" +
                    "</fetch>";

                Entity FullWorkOrder = service.RetrieveMultiple(new FetchExpression(fetchXMLWorkOrder)).Entities[0];


                //if (FullWorkOrder.Contains("bcs_containerserialno"))
                if (ServiceTask.Contains("bcs_containerno"))
                    {
                        Entity InventoryTrackingEntity = new Entity("bcs_inventorytracking");

                    DateTime utc = DateTime.UtcNow;
                    TimeZoneInfo mountainZone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                    DateTime mountainTime = TimeZoneInfo.ConvertTimeFromUtc(utc, mountainZone);

                    InventoryTrackingEntity["bcs_transactiondate"] = mountainTime;


                    if (((EntityReference)ServiceTask["msdyn_tasktype"]).Id.ToString().ToUpper() =="F284F93E-5AB7-EC11-983F-000D3A5ACF6C")//pickup
                    {
                        InventoryTrackingEntity["bcs_currentlocation"] = new EntityReference("bcs_location", new Guid("58A4958A-C839-ED11-9DB0-000D3A5C23EA"));//truck lcoation

                    }

                    else if (((EntityReference)ServiceTask["msdyn_tasktype"]).Id.ToString().ToUpper() == "3716E5B7-56BF-EC11-983E-0022480ABBD6")//dropoff
                    {
                        if (FullWorkOrder.Contains("bcs_location"))
                        {
                            InventoryTrackingEntity["bcs_currentlocation"] = FullWorkOrder["bcs_location"];
                        }
                    }

                    //InventoryTrackingEntity["bcs_containerserialno"] = FullWorkOrder["bcs_containerserialno"];
                    //EntityReference container = (EntityReference)FullWorkOrder["bcs_containerserialno"];
                    InventoryTrackingEntity["bcs_containerserialno"] = ServiceTask["bcs_containerno"];
                    EntityReference container = (EntityReference)ServiceTask["bcs_containerno"];


                    string fetchXMLInventoryTracking = @"<fetch>" +
                        "<entity name = 'bcs_inventorytracking'>" +
                        "<filter>" +
                        "<condition attribute = 'bcs_containerserialno' operator= 'eq' value ='" + container.Id + "'/>" +
                        "</filter>" +
                        "<order attribute='bcs_transactiondate' descending='true'/>" +
                        "</entity>" +
                        "</fetch>";

                    EntityCollection InventoryTrackings = service.RetrieveMultiple(new FetchExpression(fetchXMLInventoryTracking));

                    if (InventoryTrackings.Entities.Count > 0)
                    {
                        Entity LastInventoryTrack = InventoryTrackings.Entities[0];
                        if (LastInventoryTrack.Contains("bcs_currentlocation"))
                        {
                            InventoryTrackingEntity["bcs_previouslocation"] = LastInventoryTrack["bcs_currentlocation"];
                        }

                        foreach (Entity e in InventoryTrackings.Entities)
                        {
                            e["bcs_iscurrent"] = false;
                            service.Update(e);
                        }
                    }
                    InventoryTrackingEntity["bcs_iscurrent"] = true;
                    service.Create(InventoryTrackingEntity);
                }
            }
        }
    }
}
