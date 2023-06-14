using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;

namespace Library
{
    public class SharedMethods
    {
        public static void TransitionToError(PaProfileLoadDomHelper helper, string status)
        {
            switch (status)
            {
                case "draft":
                    helper.TransitionState("draft_to_ready");
                    helper.TransitionState("ready_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "ready":
                    helper.TransitionState("ready_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "in_progress":
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "active":
                    helper.TransitionState("active_to_reprovision");
                    helper.TransitionState("reprovision_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "deactivate":
                    helper.TransitionState("deactivate_to_deactivating");
                    helper.TransitionState("deactivating_to_error");
                    break;
                case "deactivating":
                    helper.TransitionState("deactivating_to_error");
                    break;
                case "reprovision":
                    helper.TransitionState("reprovision_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "complete":
                    helper.TransitionState("complete_to_ready");
                    helper.TransitionState("ready_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "active_with_errors":
                    helper.TransitionState("activewitherrors_to_deactivate");
                    helper.TransitionState("deactivate_to_deactivating");
                    helper.TransitionState("deactivating_to_error");
                    break;
            }
        }

        public static bool CheckStateChange(Engine engine, List<DomInstance> instances)
        {
            var instance = instances.First();

            engine.GenerateInformation(DateTime.Now + "|ts instance " + instance.ID.Id + " with status: " + instance.StatusId);
            if (instance.StatusId == "active" || instance.StatusId == "complete" || instance.StatusId == "active_with_errors" || instance.StatusId == "error")
            {
                return true;
            }

            return false;
        }
    }
}
