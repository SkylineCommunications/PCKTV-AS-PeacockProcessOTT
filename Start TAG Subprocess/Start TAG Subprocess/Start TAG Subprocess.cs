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
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Helpers.Logging;
using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
using Skyline.DataMiner.ExceptionHelper;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.Sections;
using static Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager.PaManagers;

/// <summary>
/// DataMiner Script Class.
/// </summary>
public class Script
{
	private DomHelper innerDomHelper;

	/// <summary>
	/// The Script entry point.
	/// </summary>
	/// <param name="engine">Link with SLAutomation process.</param>
	public void Run(Engine engine)
	{
		var scriptName = "PA_PCK_Start TAG Subprocess";
		var provisionName = String.Empty;
		var helper = new PaProfileLoadDomHelper(engine);
		innerDomHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");
		var exceptionHelper = new ExceptionHelper(engine, innerDomHelper);

		try
		{
			var tagInstanceId = helper.GetParameterValue<Guid>("TAG (Peacock)");
			var peacockInstanceId = helper.GetParameterValue<string>("InstanceId (Peacock)");
			var action = helper.GetParameterValue<string>("Action (Peacock)");
			provisionName = helper.GetParameterValue<string>("Provision Name (Peacock)");
			engine.GenerateInformation("Starting TAG Subprocess");

			if (action.Equals("reprovision"))
			{
				action = "deactivate";
			}


			var tagFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(tagInstanceId));
			var tagInstances = innerDomHelper.DomInstances.Read(tagFilter);

			if (tagInstances.Any())
			{
				ExecuteActionOnInstance(engine, action, tagInstances.First());
			}
			else
			{
				engine.GenerateInformation("No tag instances found to provision, skipping");
			}

			var peacockFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(peacockInstanceId)));
			var peacockInstance = innerDomHelper.DomInstances.Read(peacockFilter).First();
			try
			{
				if ((action == "provision" || action == "complete-provision") && peacockInstance.StatusId == "ready")
				{
					helper.TransitionState("ready_to_inprogress");
				}
				else if (action == "deactivate" && peacockInstance.StatusId == "deactivate")
				{
					helper.TransitionState("deactivate_to_deactivating");
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation("Failed to transition main state in Start TAG Subprocess: " + ex);

				var log = new Log
				{
					AffectedItem = "TAG",
					AffectedService = provisionName,
					Timestamp = DateTime.Now,
					LogNotes = ex.ToString(),
					ErrorCode = new ErrorCode
					{
						ConfigurationItem = scriptName + " Script",
						ConfigurationType = ErrorCode.ConfigType.Automation,
						Severity = ErrorCode.SeverityType.Major,
						Source = "Run() - inner exception",
					},
				};
				exceptionHelper.ProcessException(ex, log);
				helper.ReturnSuccess();
			}

			helper.ReturnSuccess();
		}
		catch (Exception ex)
		{
			engine.GenerateInformation("Error starting TAG process: " + ex);

			var log = new Log
			{
				AffectedItem = "TAG",
				AffectedService = provisionName,
				Timestamp = DateTime.Now,
				LogNotes = ex.ToString(),
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

	private void ExecuteActionOnInstance(Engine engine, string action, DomInstance instance)
	{
		var status = instance.StatusId;

		if (status == "active" || status == "complete" || status == "draft")
		{
			innerDomHelper.DomInstances.ExecuteAction(instance.ID, action);
		}
		else if (status.StartsWith("error"))
		{
			innerDomHelper.DomInstances.ExecuteAction(instance.ID, "error-" + action);
		}
		else
		{
			innerDomHelper.DomInstances.ExecuteAction(instance.ID, "activewitherrors-" + action);
		}
	}

	private SectionDefinition SetSectionDefinitionById(SectionDefinitionID sectionDefinitionId)
	{
		return innerDomHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(sectionDefinitionId)).First();
	}
}