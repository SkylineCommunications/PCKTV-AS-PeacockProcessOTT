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

04/01/2022  1.0.0.1     JDI, Skyline    Initial Version
03/03/2022  1.0.0.2     PVP, Skyline    Add logging example. [DCP178986]

****************************************************************************
*/

namespace PA.ProfileLoadDomTemplate
{
	using System;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.Remoting.Messaging;
	using System.Threading;
	using Helper;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Helpers.Logging;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
	using Skyline.DataMiner.ExceptionHelper;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.LogHelpers;

	public static class Status
	{
		public static readonly string isError = "Error";
		public static readonly string isActive = "Active";
		public static readonly string isActiveWithErrors = "Active with Errors";
	}
	internal class Script
	{
		/// <summary>
		/// The Script entry point.
		/// </summary>
		/// <param name="engine">The <see cref="Engine" /> instance used to communicate with DataMiner.</param>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "Ignored")]
		public void Run(Engine engine)
		{
			var scriptName = "PA_PCK_Evaluate Event";
			var helper = new PaProfileLoadDomHelper(engine);
			var domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");
			var exceptionHelper = new ExceptionHelper(engine, domHelper);
			var provisionName = helper.GetParameterValue<string>("Provision Name (Peacock)");
			var maindomInstance = helper.GetParameterValue<string>("InstanceId (Peacock)");
			var mainFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(maindomInstance)));
			var mainInstance = domHelper.DomInstances.Read(mainFilter).First();
			engine.GenerateInformation("START " + scriptName);
			helper.Log("START " + scriptName, PaLogLevel.Information);

			if (mainInstance.StatusId == "ready")
			{
				helper.TransitionState("ready_to_active");
			}

			CheckChildStatus(engine, helper, domHelper, mainInstance, exceptionHelper, provisionName);

			try
			{
				// var touchstreamId = helper.GetParameterValue<Guid>("Touchstream");
				// var tagId = helper.GetParameterValue<Guid>("TAG");

				// var mainFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(maindomInstance));
				// var mainInstance = domHelper.DomInstances.Read(filter).First();

				// var convivaFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(convivaId));
				// var convivaInstance = domHelper.DomInstances.Read(convivaFilter).First();

				// var touchstreamFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(touchstreamId));
				// var touchstreamInstance = domHelper.DomInstances.Read(touchstreamFilter).First();

				// var tagFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(tagId));
				// var tagInstance = domHelper.DomInstances.Read(tagFilter).First();

				// check statuses via tagInstance.StatusId == "complete" etc
				// Thread.Sleep(5000);
				var sourceElement = helper.GetParameterValue<string>("Source Element (Peacock)");
				var eventId = helper.GetParameterValue<string>("Event ID (Peacock)");
				if (!string.IsNullOrWhiteSpace(sourceElement))
				{
					ExternalRequest evtmgrUpdate = new ExternalRequest
					{
						Type = "Process Automation",
						ProcessResponse = new ProcessResponse
						{
							EventName = eventId,
							Peacock = new PeacockResponse
							{
								Status = mainInstance.StatusId == "in_progress" ? "Active" : "Complete",
							},
						},
					};

					var elementSplit = sourceElement.Split('/');
					var eventManager = engine.FindElement(Convert.ToInt32(elementSplit[0]), Convert.ToInt32(elementSplit[1]));
					eventManager.SetParameter(999, JsonConvert.SerializeObject(evtmgrUpdate));
				}

				helper.Log("Finished Evaluate Event.", PaLogLevel.Debug);
				helper.SendFinishMessageToTokenHandler();
			}
			catch (Exception ex)
			{
				helper.Log($"An issue occurred while evaluating the event: {ex}", PaLogLevel.Error);
				engine.GenerateInformation("exception in evaluate event: " + ex);

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

				// helper.SendErrorMessageToTokenHandler();
				helper.SendFinishMessageToTokenHandler();
			}
		}

		private void CheckChildStatus(
			Engine engine,
			PaProfileLoadDomHelper helper,
			DomHelper domHelper,
			DomInstance mainInstance,
			ExceptionHelper exceptionHelper,
			string provisionName)
		{
			var scriptName = "PA_PCK_Evaluate Event";

			var tagId = helper.GetParameterValue<Guid>("TAG (Peacock)");
			var touchstreamId = helper.GetParameterValue<Guid>("Touchstream (Peacock)");
			var convivaId = helper.GetParameterValue<Guid>("Conviva (Peacock)");

			var tagFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(tagId));
			var tagInstance = domHelper.DomInstances.Read(tagFilter).First();

			var touchstreamFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(touchstreamId));
			var touchstreamInstance = domHelper.DomInstances.Read(touchstreamFilter).First();

			var convivaFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(convivaId));
			var convivaInstance = domHelper.DomInstances.Read(convivaFilter).First();

			var tagStatus = tagInstance.StatusId;
			var touchstreamStatus = touchstreamInstance.StatusId;
			var convivaStatus = convivaInstance.StatusId;

			engine.GenerateInformation("main status: " + mainInstance.StatusId);
			if (mainInstance.StatusId == "in_progress")
			{
				if (tagStatus == Status.isError && touchstreamStatus == Status.isError && convivaStatus == Status.isError)
				{
					helper.TransitionState("inprogress_to_error");

					var log = new Log
					{
						AffectedItem = scriptName,
						AffectedService = provisionName,
						Timestamp = DateTime.Now,
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Source = "CheckChildStatus() method",
							Code = "ChildStatusInErrorState",
							Severity = ErrorCode.SeverityType.Major,
							Description = $"Child Status In Error State | TAG Status: {tagStatus} | Conviva Status: {convivaStatus} | Touchstream Status: {touchstreamStatus}",
						},
						SummaryFlag = true,
					};
					exceptionHelper.GenerateLog(log);
				}
				else if (tagStatus != Status.isActive || touchstreamStatus != Status.isActive || convivaStatus != Status.isActive)
				{
					helper.TransitionState("inprogress_to_activewitherrors");

					var log = new Log
					{
						AffectedItem = scriptName,
						AffectedService = provisionName,
						Timestamp = DateTime.Now,
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Source = "CheckChildStatus() method",
							Code = "ChildFailedProvisioning",
							Severity = ErrorCode.SeverityType.Major,
							Description = $"At least one child failed provisioning | TAG Status: {tagStatus} | Conviva Status: {convivaStatus} | Touchstream Status: {touchstreamStatus}",
						},
						SummaryFlag = true,
					};
					exceptionHelper.GenerateLog(log);
				}
				else
				{
					helper.TransitionState("inprogress_to_active");
				}
			}
			else if (mainInstance.StatusId == "deactivating")
			{
				if (tagStatus == Status.isError && touchstreamStatus == Status.isError && convivaStatus == Status.isError)
				{
					helper.TransitionState("deactivating_to_error");

					var log = new Log
					{
						AffectedItem = scriptName,
						AffectedService = provisionName,
						Timestamp = DateTime.Now,
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Source = "CheckChildStatus()",
							Code = "PeacockDeactivatingFailed",
							Severity = ErrorCode.SeverityType.Major,
							Description = $"Failed to Deactivate | Child Status In Error State | TAG Status: {tagStatus} | Conviva Status: {convivaStatus} | Touchstream Status: {touchstreamStatus}",
						},
						SummaryFlag = true,
					};
					exceptionHelper.GenerateLog(log);
				}
				else if (tagStatus != Status.isActive || touchstreamStatus != Status.isActive || convivaStatus != Status.isActive)
				{
					helper.TransitionState("deactivating_to_activewitherros");

					var log = new Log
					{
						AffectedItem = scriptName,
						AffectedService = provisionName,
						Timestamp = DateTime.Now,
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Source = "CheckChildStatus()",
							Code = "ChildFailedDeactivating",
							Severity = ErrorCode.SeverityType.Major,
							Description = $"At least one child failed deactivating | TAG Status: {tagStatus} | Conviva Status: {convivaStatus} | Touchstream Status: {touchstreamStatus}",
						},
						SummaryFlag = true,
					};
					exceptionHelper.GenerateLog(log);
				}
				else
				{
					helper.TransitionState("deactivating_to_complete");
				}
			}
			else
			{
				engine.GenerateInformation("Unknown main instance status: " + mainInstance.StatusId);
			}
		}
	}
}