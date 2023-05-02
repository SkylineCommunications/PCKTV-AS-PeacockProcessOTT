/*
****************************************************************************
*  Copyright (c) 2022,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

    Skyline Communications NV
    Ambachtenstraat 33
    B-8870 Izegem
    Belgium
    Tel.    : +32 51 31 35 69
    Fax.    : +32 51 31 01 29
    E-mail  : info@skyline.be
    Web     : www.skyline.be
    Contact : Ben Vandenberghe

****************************************************************************
Revision History:

DATE        VERSION     AUTHOR          COMMENTS

dd/mm/2022  1.0.0.1     XXX, Skyline    Initial version
****************************************************************************
*/

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Helpers.Logging;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.Messages.SLDataGateway;

/// <summary>
/// DataMiner Script Class.
/// </summary>
public class Script
{
    /// <summary>
    /// The Script entry point.
    /// </summary>
    /// <param name="engine">Link with SLAutomation process.</param>
    public void Run(Engine engine)
    {
        var helper = new PaProfileLoadDomHelper(engine);
        try
        {
            // gathering instance id from parent is the challenge
            var tagInstanceId = helper.GetParameterValue<Guid>("TAG (Peacock)");
            var domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");

            var tagFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(tagInstanceId));
            var tagInstances = domHelper.DomInstances.Read(tagFilter);

            if (!tagInstances.Any())
            {
                engine.GenerateInformation("No tag instances found to provision, skipping");
                helper.Log("Finished Waiting TAG Subprocess.", PaLogLevel.Debug);
                helper.ReturnSuccess();
                return;
            }

            bool CheckStateChange()
            {
                try
                {
                    var filter = DomInstanceExposers.Id.Equal(new DomInstanceId(tagInstanceId));
                    tagInstances = domHelper.DomInstances.Read(filter);
                    if (tagInstances.Count == 0)
                    {
                        // returning success until conviva is ready
                        throw new NullReferenceException("No TAG DOM instance found with id: " + tagInstanceId);
                    }

                    var tagInstance = tagInstances.First();

                    engine.GenerateInformation(DateTime.Now + "|tag instance " + tagInstance.ID.Id + " with status: " + tagInstance.StatusId);
                    if (tagInstance.StatusId == "active" || tagInstance.StatusId == "complete")
                    {
                        return true;
                    }

                    return false;
                }
                catch (Exception e)
                {
                    engine.GenerateInformation("Exception thrown while verifying the subprocess: " + e);
                    throw;
                }
            }

            if (Retry(CheckStateChange, new TimeSpan(0, 10, 0)))
            {
                // successfully created filter
                engine.GenerateInformation("TAG process dom reports complete");

                helper.Log("Finished Waiting TAG Subprocess.", PaLogLevel.Debug);
                helper.ReturnSuccess();
            }
            else
            {
                // failed to execute in time
                engine.GenerateInformation("Verifying TAG subprocess took longer than expected and could not verify.");
                helper.ReturnSuccess();
            }
        }
        catch (Exception ex)
        {
            engine.GenerateInformation("Exception occurred in Waiting TAG Subprocess: " + ex);
        }
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