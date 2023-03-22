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
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2022	1.0.0.1		XXX, Skyline	Initial version
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
using Helper;

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
			var subdomInstance = helper.GetParameterValue<Guid>("Conviva");
			var domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");

			bool CheckStateChange()
			{
				try
				{
					// data
					var filter = DomInstanceExposers.Id.Equal(new DomInstanceId(subdomInstance));
					var subInstances = domHelper.DomInstances.Read(filter);
					if (subInstances.Count == 0)
					{
						// returning success until conviva is ready
						return true;
					}

					var instance = subInstances.First();

					engine.GenerateInformation(DateTime.Now + "|instance " + instance.ID.Id + " with status: " + instance.StatusId);
					if (instance.StatusId == "active" || instance.StatusId == "complete")
					{
						return true;
					}

					return false;
				}
				catch (Exception e)
				{
					engine.Log("Exception thrown while verifying the subprocess: " + e);
					throw;
				}
			}

			if (Retry(CheckStateChange, new TimeSpan(0, 10, 0)))
			{
				var filter = DomInstanceExposers.Id.Equal(new DomInstanceId(subdomInstance));
				var subInstances = domHelper.DomInstances.Read(filter);
				var instance = subInstances.First();

				// successfully created filter
				engine.GenerateInformation("Conviva process dom reports complete");
				var sourceElement = helper.GetParameterValue<string>("Source Element");
				var provisionName = helper.GetParameterValue<string>("Provision Name");
				if (!String.IsNullOrWhiteSpace(sourceElement))
				{
					ExternalRequest evtmgrUpdate = new ExternalRequest
					{
						Type = "Process Automation",
						ProcessResponse = new ProcessResponse
						{
							EventName = provisionName,
							Conviva = new ConvivaResponse
							{
								Status = instance.StatusId == "active" ? "Active" : "Complete",
							},
						},
					};

					var elementSplit = sourceElement.Split('/');
					var eventManager = engine.FindElement(Convert.ToInt32(elementSplit[0]), Convert.ToInt32(elementSplit[1]));
					eventManager.SetParameter(999, JsonConvert.SerializeObject(evtmgrUpdate));
				}

				helper.ReturnSuccess();
			}
			else
			{
				// failed to execute in time
			}
		}
		catch (Exception ex)
		{
			engine.Log("Error: " + ex);
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