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
using Helper;
using Library;
using Newtonsoft.Json;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Helpers.Logging;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
using Skyline.DataMiner.ExceptionHelper;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.ProtoBuf.Shared.Core.DataTypes.Shared.V1;
using SLLoggerUtil;

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
		var scriptName = "VPA_PCK_Verify Touchstream Provision";
		var provisionName = String.Empty;
		var helper = new PaProfileLoadDomHelper(engine);
		var domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");
		var exceptionHelper = new ExceptionHelper(engine, domHelper);
		engine.GenerateInformation($"START {scriptName}");

		try
		{
			var touchstreamInstanceId = helper.GetParameterValue<Guid>("Touchstream (Peacock)");
			provisionName = helper.GetParameterValue<string>("Provision Name (Peacock)");

			var touchstreamFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(touchstreamInstanceId));
			var touchstreamInstances = domHelper.DomInstances.Read(touchstreamFilter);

			if (!touchstreamInstances.Any())
			{
				engine.GenerateInformation("No touchstream instances provisioned, skipping.");
				helper.ReturnSuccess();
				return;
			}


			bool CheckStateChange()
			{
				try
				{
					return SharedMethods.CheckStateChange(domHelper, touchstreamFilter);
				}
				catch (Exception e)
				{
					engine.GenerateInformation("Exception thrown while verifying the subprocess: " + e);
					var log = new Log
					{
						AffectedItem = "Touchstream Subprocess",
						AffectedService = provisionName,
						Timestamp = DateTime.Now,
						LogNotes = $"Exception thrown while verifying Touchstream {provisionName} provision status.",
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Severity = ErrorCode.SeverityType.Warning,
							Source = "CheckStateChange()",
							Code = "ExceptionCheckingStatus",
							Description = "Exception thrown While Checking Status.",
						},
					};
					exceptionHelper.GenerateLog(log);
					helper.ReturnSuccess();
					throw;
				}
			}

			if (SharedMethods.Retry(CheckStateChange, new TimeSpan(0, 10, 0)))
			{
				engine.GenerateInformation("Finished Verify Touchstream Provision.");
				helper.ReturnSuccess();
			}
			else
			{
				// failed to execute in time
				engine.GenerateInformation("Verifying Touchstream provision took longer than expected and could not verify status.");

				var log = new Log
				{
					AffectedItem = "Touchstream Subprocess",
					AffectedService = provisionName,
					Timestamp = DateTime.Now,
					LogNotes = $"Verifying Touchstream {provisionName} provision took longer than expected and could not verify status.",
					ErrorCode = new ErrorCode
					{
						ConfigurationItem = scriptName + " Script",
						ConfigurationType = ErrorCode.ConfigType.Automation,
						Severity = ErrorCode.SeverityType.Warning,
						Source = "Retry()",
						Code = "RetryTimeout",
						Description = "Verifying provision took longer than expected.",
					},
				};
				exceptionHelper.GenerateLog(log);

				helper.ReturnSuccess();
			}
		}
		catch (Exception ex)
		{
			engine.GenerateInformation("Exception occurred in Verify Touchstream Provision: " + ex);

			var log = new Log
			{
				AffectedItem = scriptName,
				AffectedService = provisionName,
				Timestamp = DateTime.Now,
				ErrorCode = new ErrorCode
				{
					ConfigurationItem = scriptName + " Script",
					ConfigurationType = ErrorCode.ConfigType.Automation,
					Severity = ErrorCode.SeverityType.Major,
					Source = "Run()",
				},
			};
			exceptionHelper.ProcessException(ex, log);
			helper.ReturnSuccess();
		}
	}
}