using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using System.Diagnostics;
using System.Threading;
using Skyline.DataMiner.Net.LogHelpers;
using Skyline.DataMiner.Net.Messages.SLDataGateway;

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

        public static bool CheckStateChange(DomHelper domHelper,ManagedFilter<DomInstance,Guid> filter)
        {
            var instances = domHelper.DomInstances.Read(filter);
            var instance = instances.First();

            if (instance.StatusId == "active" || instance.StatusId == "complete" || instance.StatusId == "active_with_errors" || instance.StatusId == "error")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Retry until success or until timeout.
        /// </summary>
        /// <param name="func">Operation to retry.</param>
        /// <param name="timeout">Max TimeSpan during which the operation specified in <paramref name="func"/> can be retried.</param>
        /// <returns><c>true</c> if one of the retries succeeded within the specified <paramref name="timeout"/>. Otherwise <c>false</c>.</returns>
        public static bool Retry(Func<bool> func, TimeSpan timeout)
        {
            bool success;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                success = func();
                if (!success)
                {
                    Thread.Sleep(3000);
                }
            }
            while (!success && sw.Elapsed <= timeout);

            return success;
        }
    }
}
