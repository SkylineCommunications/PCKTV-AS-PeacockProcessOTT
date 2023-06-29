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
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.Remoting.Messaging;
	using System.Threading;
	using Helper;
	using Library;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Helpers.Logging;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
	using Skyline.DataMiner.ExceptionHelper;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.LogHelpers;
	using Skyline.DataMiner.Net.Messages;

	public class Status
	{
		public static readonly string Error = "error";
		public static readonly string Active = "active";
		public static readonly string ActiveWithErrors = "active_with_errors";
		public static readonly string Complete = "complete";
		public static readonly string InProgress = "in_progress";
		public static readonly string Deactivating = "deactivating";
		public static readonly string Reprovision = "reprovision";
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
			engine.GenerateInformation("START " + scriptName);

			var mainStatus = String.Empty;
			var maindomInstance = helper.GetParameterValue<string>("InstanceId (Peacock)");
			var mainFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(maindomInstance)));
			var mainInstance = domHelper.DomInstances.Read(mainFilter).First();

			try
			{
				mainStatus = mainInstance.StatusId;

				if (mainStatus == "ready")
				{
					helper.TransitionState("ready_to_inprogress");
				}

				if (mainStatus == "deactivate")
				{
					helper.TransitionState("deactivate_to_deactivating");
					mainInstance = domHelper.DomInstances.Read(mainFilter).First();
				}

				CheckChildStatus(helper, domHelper, mainInstance, exceptionHelper, provisionName);

				// Update the instance after performing a transition
				mainFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(maindomInstance)));
				mainInstance = domHelper.DomInstances.Read(mainFilter).First();
				EventManagerCallback(engine, helper, mainInstance);

				var action = helper.GetParameterValue<string>("Action (Peacock)");
				if (action == "reprovision")
				{
					var rebuildScript = engine.PrepareSubScript("Rebuild Peacock DOM");
					rebuildScript.SelectScriptParam(1, maindomInstance);
					rebuildScript.StartScript();
				}

				helper.SendFinishMessageToTokenHandler();
			}
			catch (Exception ex)
			{
				SharedMethods.TransitionToError(helper, mainStatus);
				engine.GenerateInformation("exception in evaluate event: " + ex);
				var log = new Log
				{
					AffectedItem = "Main Provision",
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
				EventManagerCallback(engine, helper, mainInstance);
				helper.SendFinishMessageToTokenHandler();
			}
		}

		private static void EventManagerCallback(Engine engine, PaProfileLoadDomHelper helper, DomInstance mainInstance)
		{
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
							Status = mainInstance.StatusId == "complete" ? "Complete" : "Active",
						},
					},
				};

				var elementSplit = sourceElement.Split('/');
				var eventManager = engine.FindElement(Convert.ToInt32(elementSplit[0]), Convert.ToInt32(elementSplit[1]));
				eventManager.SetParameter(999, JsonConvert.SerializeObject(evtmgrUpdate));
			}
		}

		private void CheckChildStatus(
			PaProfileLoadDomHelper helper,
			DomHelper domHelper,
			DomInstance mainInstance,
			ExceptionHelper exceptionHelper,
			string provisionName)
		{
			var tagId = helper.GetParameterValue<Guid>("TAG (Peacock)");
			var touchstreamId = helper.GetParameterValue<Guid>("Touchstream (Peacock)");
			var convivaId = helper.GetParameterValue<Guid>("Conviva (Peacock)");

			var tagStatus = GetChildInstanceStatus(tagId, domHelper, mainInstance.StatusId);
			var touchstreamStatus = GetChildInstanceStatus(touchstreamId, domHelper, mainInstance.StatusId);
			var convivaStatus = GetChildInstanceStatus(convivaId, domHelper, mainInstance.StatusId);

			if (mainInstance.StatusId == "in_progress")
			{
				var description = $"Failed to Provision Service";

				if (tagStatus == Status.Active && touchstreamStatus == Status.Active && convivaStatus == Status.Active)
				{
					helper.TransitionState("inprogress_to_active");
				}
				else if (tagStatus == Status.Error && touchstreamStatus == Status.Error && convivaStatus == Status.Error)
				{
					SharedMethods.TransitionToError(helper, mainInstance.StatusId);
					var affectedItem = "TAG - Conviva - Touchstream";
					var code = "SubprocessStatusInErrorState";
					var logNotes = $"Failed to provision event {mainInstance.Name} due to all subprocess in ERROR state";
					var log = CreateLog(affectedItem, provisionName, code, description, logNotes);
					exceptionHelper.GenerateLog(log);
				}
				else
				{
					var affectedItem = GetAffectedItems(tagStatus, convivaStatus, touchstreamStatus);
					var code = "SubprocessFailedToProvision";
					var logNotes = $"At least one subprocess failed to provision {mainInstance.Name}. Subprocess status TAG: {tagStatus}. Conviva: {convivaStatus}. Touchstream: {touchstreamStatus} ";
					var log = CreateLog(affectedItem, provisionName, code, description, logNotes);
					exceptionHelper.GenerateLog(log);
					helper.TransitionState("inprogress_to_activewitherrors");
				}
			}
			else if (mainInstance.StatusId == "deactivating")
			{
				var description = $"Failed to Deactivate Service";

				if (tagStatus == Status.Complete && touchstreamStatus == Status.Complete && convivaStatus == Status.Complete)
				{
					helper.TransitionState("deactivating_to_complete");
				}
				else if (tagStatus == Status.Error && touchstreamStatus == Status.Error && convivaStatus == Status.Error)
				{
					SharedMethods.TransitionToError(helper, mainInstance.StatusId);
					var affectedItem = "TAG - Conviva - Touchstream";
					var code = "MainInstanceDeactivationFailed";
					var logNotes = $"Failed to deactivate event {mainInstance.Name} due to all subprocess in ERROR state";
					var log = CreateLog(affectedItem, provisionName, code, description, logNotes);
					exceptionHelper.GenerateLog(log);
				}
				else
				{
					var affectedItem = GetAffectedItems(tagStatus, convivaStatus, touchstreamStatus);
					SharedMethods.TransitionToError(helper, mainInstance.StatusId);
					var code = "SubprocessdFailedToDeactivate";
					var logNotes = $"At least one subprocess failed to deactivate {mainInstance.Name}. Subprocess status TAG: {tagStatus}. Conviva: {convivaStatus}. Touchstream: {touchstreamStatus} ";
					var log = CreateLog(affectedItem, provisionName, code, description, logNotes);
					exceptionHelper.GenerateLog(log);
				}
			}
			else
			{
				//reprovision - no action needed

				if (!mainInstance.StatusId.Equals("reprovision"))
				{
					var affectedItem = "Main Provision";
					var code = "UnknownStatus";
					var description = $"Main Instance Unknown Status";
					var logNotes = $"Unkown status for event {mainInstance.Name}. Current status: {mainInstance.StatusId}";
					SharedMethods.TransitionToError(helper, mainInstance.StatusId);
					var log = CreateLog(affectedItem, provisionName, code, description, logNotes);
					exceptionHelper.GenerateLog(log);
				}
			}
		}

		private Log CreateLog(string affectedItem, string provisionName, string code, string description, string logNotes)
		{
			var log = new Log
			{
				AffectedItem = affectedItem,
				AffectedService = provisionName,
				Timestamp = DateTime.Now,
				LogNotes = logNotes,
				ErrorCode = new ErrorCode
				{
					ConfigurationItem = "PA_PCK_Evaluate Event Script",
					ConfigurationType = ErrorCode.ConfigType.Automation,
					Source = "CheckChildStatus()",
					Code = code,
					Severity = ErrorCode.SeverityType.Major,
					Description = description,
				},
				SummaryFlag = true,
			};

			return log;
		}

		private string GetChildInstanceStatus(Guid childId, DomHelper domHelper, string mainInstanceStatus)
		{
			var childFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(childId));
			var childInstance = domHelper.DomInstances.Read(childFilter);

			if (!childInstance.Any() && mainInstanceStatus == Status.InProgress)
			{
				return Status.Active;
			}

			if (!childInstance.Any() && (mainInstanceStatus == Status.Deactivating || mainInstanceStatus == Status.Reprovision))
			{
				return Status.Complete;
			}

			var instance = childInstance.First();
			return instance.StatusId;
		}

		private string GetAffectedItems(string tagStatus, string convivaStatus, string touchstreamStatus)
		{
			List<string> affectedItems = new List<string>();

			if (tagStatus == Status.Error)
			{
				affectedItems.Add("TAG");
			}

			if (convivaStatus == Status.Error)
			{
				affectedItems.Add("Conviva");
			}

			if (touchstreamStatus == Status.Error)
			{
				affectedItems.Add("Touchstream");

			}

			var items = string.Join(" - ", affectedItems);

			return items;
		}
	}
}