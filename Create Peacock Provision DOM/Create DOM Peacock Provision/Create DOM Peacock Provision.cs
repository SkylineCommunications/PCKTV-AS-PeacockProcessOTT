/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
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

dd/mm/2023  1.0.0.1     XXX, Skyline    Initial version
****************************************************************************
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Actions;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Buttons;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Concatenation;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Conditions;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using Skyline.DataMiner.Net.Sections;

/// <summary>
/// DataMiner Script Class.
/// </summary>
public class Script
{
	private static readonly string BehaviorDefinitionName = "Peacock Provision Behavior";
	private static readonly string DefinitionName = "Peacock Provision";
	private Engine engine;
	private DomHelper domHelper;

	/// <summary>
	/// The Script entry point.
	/// </summary>
	/// <param name="engine">Link with SLAutomation process.</param>
	public void Run(Engine engine)
	{
		this.engine = engine;
		domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");

		var peacockProvisionDomDefinition = CreateDomDefinition();
		if (peacockProvisionDomDefinition != null)
		{
			var domDefinition = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal(peacockProvisionDomDefinition.Name));
			if (domDefinition.Any())
			{
				peacockProvisionDomDefinition.ID = domDefinition.FirstOrDefault()?.ID;
				domHelper.DomDefinitions.Update(peacockProvisionDomDefinition);
			}
			else
			{
				domHelper.DomDefinitions.Create(peacockProvisionDomDefinition);
			}
		}
	}

	private DomDefinition CreateDomDefinition()
	{
		try
		{
			// Create SectionDefinitions
			var nameDescriptor = new FieldDescriptorID();
			var provisionInfoSectionDefinitions = SectionDefinitions.CreateProvisionInfoServiceDefinition(domHelper, ref nameDescriptor);
			var domInstancesSectionDefinitions = SectionDefinitions.CreateDomInstancesServiceDefinition(engine, domHelper);

			var sections = new List<SectionDefinition> { provisionInfoSectionDefinitions, domInstancesSectionDefinitions };

			// Create DomBehaviorDefinition
			var domBehaviorDefinition = BehaviorDefinitions.CreateDomBehaviorDefinition(sections);
			CreateOrUpdateDomBehaviorDefinition(domBehaviorDefinition);

			var nameDefinition = new ModuleSettingsOverrides
			{
				NameDefinition = new DomInstanceNameDefinition
				{
					ConcatenationItems = new List<IDomInstanceConcatenationItem>
					{
						new FieldValueConcatenationItem
						{
							FieldDescriptorId = nameDescriptor,
						},
					},
				},
			};

			return new DomDefinition
			{
				Name = DefinitionName,
				SectionDefinitionLinks = new List<SectionDefinitionLink> { new SectionDefinitionLink(provisionInfoSectionDefinitions.GetID()), new SectionDefinitionLink(domInstancesSectionDefinitions.GetID()) },
				DomBehaviorDefinitionId = domBehaviorDefinition.ID,
				ModuleSettingsOverrides = nameDefinition,
			};
		}
		catch (Exception ex)
		{
			engine.GenerateInformation($"Error on CreateDomDefinition method with exception: {ex}");
			return null;
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Ignored")]
	public class SectionDefinitions
	{
		public static SectionDefinition CreateDomInstancesServiceDefinition(Engine engine, DomHelper domHelper)
		{
			var convivaDefinitionId = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal("Conviva")).First().ID.Id;
			var tagDefinitionId = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal("TAG")).First().ID.Id;
			var tsDefinitionId = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal("Touchstream")).First().ID.Id;

			var convivaFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("Conviva (Peacock)", "Link to the DOM Instance that contains the information for Conviva provisioning.", convivaDefinitionId);
			var tagFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("TAG (Peacock)", "Link to the DOM Instance that contains the information for TAG provisioning.", tagDefinitionId);
			var touchstreamFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("Touchstream (Peacock)", "Link to the DOM Instance that contains the information for TS provisioning.", tsDefinitionId);

			List<FieldDescriptor> fieldDescriptors = new List<FieldDescriptor>
			{
				convivaFieldDescriptor,
				tagFieldDescriptor,
				touchstreamFieldDescriptor,
			};

			var domInstanceSection = CreateOrUpdateSection("DOM Instances", domHelper, fieldDescriptors);

			return domInstanceSection;
		}

		public static SectionDefinition CreateProvisionInfoServiceDefinition(DomHelper domHelper, ref FieldDescriptorID nameDescriptor)
		{
			var provisionNameFieldDescriptor = CreateFieldDescriptorObject<string>("Provision Name (Peacock)", "A name to describe the Event or Channel being provisioned.");
			var eventIdFieldDescriptor = CreateFieldDescriptorObject<string>("Event ID (Peacock)", "Unique ID to link the provision to an Event or Channel.");
			var sourceElementFieldDescriptor = CreateFieldDescriptorObject<string>("Source Element (Peacock)", "A DMAID/ELEMID/PID that has been configured to receive process updates (if configured).");
			var instanceFieldDescriptor = CreateFieldDescriptorObject<string>("InstanceId (Peacock)", "The id of the DOM instance.");
			var actionFieldDescriptor = CreateFieldDescriptorObject<string>("Action (Peacock)", "The action to be executed on this provision.");

			nameDescriptor = provisionNameFieldDescriptor.ID;

			List<FieldDescriptor> fieldDescriptors = new List<FieldDescriptor>
			{
				provisionNameFieldDescriptor,
				eventIdFieldDescriptor,
				sourceElementFieldDescriptor,
				instanceFieldDescriptor,
				actionFieldDescriptor,
			};

			var provisionInfoSection = CreateOrUpdateSection("Provision Info", domHelper, fieldDescriptors);

			return provisionInfoSection;
		}

		private static SectionDefinition CreateOrUpdateSection(string name, DomHelper domHelper, List<FieldDescriptor> fieldDescriptors)
		{
			var domInstancesSectionDefinition = new CustomSectionDefinition
			{
				Name = name,
			};

			var domInstanceSection = domHelper.SectionDefinitions.Read(SectionDefinitionExposers.Name.Equal(domInstancesSectionDefinition.Name));
			SectionDefinition sectionDefinition;
			if (!domInstanceSection.Any())
			{
				foreach (var field in fieldDescriptors)
				{
					domInstancesSectionDefinition.AddOrReplaceFieldDescriptor(field);
				}

				sectionDefinition = domHelper.SectionDefinitions.Create(domInstancesSectionDefinition) as CustomSectionDefinition;
			}
			else
			{
				// Update Section Definition (Add missing fieldDescriptors)
				sectionDefinition = UpdateSectionDefinition(domHelper, fieldDescriptors, domInstanceSection);
			}

			return sectionDefinition;
		}

		private static SectionDefinition UpdateSectionDefinition(DomHelper domHelper, List<FieldDescriptor> fieldDescriptorList, List<SectionDefinition> sectionDefinition)
		{
			var existingSectionDefinition = sectionDefinition.First() as CustomSectionDefinition;
			var previousFieldNames = existingSectionDefinition.GetAllFieldDescriptors().Select(x => x.Name).ToList();
			List<FieldDescriptor> fieldDescriptorsToAdd = new List<FieldDescriptor>();

			// Check if there's a fieldDefinition to add
			foreach (var newfieldDescriptor in fieldDescriptorList)
			{
				if (!previousFieldNames.Contains(newfieldDescriptor.Name))
				{
					fieldDescriptorsToAdd.Add(newfieldDescriptor);
				}
			}

			if (fieldDescriptorsToAdd.Count > 0)
			{
				foreach (var field in fieldDescriptorsToAdd)
				{
					existingSectionDefinition.AddOrReplaceFieldDescriptor(field);
				}

				existingSectionDefinition = domHelper.SectionDefinitions.Update(existingSectionDefinition) as CustomSectionDefinition;
			}

			return existingSectionDefinition;
		}

		private static FieldDescriptor CreateFieldDescriptorObject<T>(string fieldName, string toolTip)
		{
			return new FieldDescriptor
			{
				FieldType = typeof(T),
				Name = fieldName,
				Tooltip = toolTip,
			};
		}

		private static DomInstanceFieldDescriptor CreateDomInstanceFieldDescriptorObject<T>(string fieldName, string toolTip, Guid definitionId)
		{
			var field = new DomInstanceFieldDescriptor("process_automation")
			{
				FieldType = typeof(T),
				Name = fieldName,
				Tooltip = toolTip,
			};

			field.DomDefinitionIds.Add(new DomDefinitionId(definitionId));
			return field;
		}
	}

	private void CreateOrUpdateDomBehaviorDefinition(DomBehaviorDefinition newDomBehaviorDefinition)
	{
		if (newDomBehaviorDefinition != null)
		{
			var domBehaviorDefinition = domHelper.DomBehaviorDefinitions.Read(DomBehaviorDefinitionExposers.Name.Equal(newDomBehaviorDefinition.Name));
			if (domBehaviorDefinition.Any())
			{
				newDomBehaviorDefinition.ID = domBehaviorDefinition.FirstOrDefault()?.ID;
				domHelper.DomBehaviorDefinitions.Update(newDomBehaviorDefinition);
			}
			else
			{
				domHelper.DomBehaviorDefinitions.Create(newDomBehaviorDefinition);
			}
		}
	}

	public class BehaviorDefinitions
	{
		public static DomBehaviorDefinition CreateDomBehaviorDefinition(List<SectionDefinition> sections)
		{
			var statuses = new List<DomStatus>
			{
				new DomStatus("draft", "Draft"),
				new DomStatus("ready", "Ready"),
				new DomStatus("in_progress", "In Progress"),
				new DomStatus("active", "Active"),
				new DomStatus("deactivate", "Deactivate"),
				new DomStatus("reprovision", "Reprovision"),
				new DomStatus("complete", "Complete"),
				new DomStatus("deactivating", "Deactivating"),
			};

			var transitions = new List<DomStatusTransition>
			{
				new DomStatusTransition("draft_to_ready", "draft", "ready"),
				new DomStatusTransition("ready_to_inprogress", "ready", "in_progress"),
				new DomStatusTransition("inprogress_to_active", "in_progress", "active"),
				new DomStatusTransition("active_to_deactivate", "active", "deactivate"),
				new DomStatusTransition("active_to_reprovision", "active", "reprovision"),
				new DomStatusTransition("deactivate_to_deactivating", "deactivate", "deactivating"),
				new DomStatusTransition("deactivating_to_complete", "deactivating", "complete"),
				new DomStatusTransition("reprovision_to_inprogress", "reprovision", "in_progress"),
				new DomStatusTransition("complete_to_ready", "complete", "ready"),
			};

			var behaviorActions = GetBehaviorActions("Peacock Process", "Provision Name");
			var buttonDefinitions = GetBehaviorButtons();

			return new DomBehaviorDefinition
			{
				Name = BehaviorDefinitionName,
				InitialStatusId = "draft",
				Statuses = statuses,
				StatusTransitions = transitions,
				StatusSectionDefinitionLinks = GetStatusLinks(sections),
				ActionDefinitions = behaviorActions,
				ButtonDefinitions = buttonDefinitions,
			};
		}

		private static List<DomStatusSectionDefinitionLink> GetStatusLinks(List<SectionDefinition> sections)
		{
			Dictionary<string, List<FieldDescriptorID>> fieldsList = GetFieldDescriptorDictionary(sections);

			var draftStatusLinks = StatusSectionDefinitions.GetDraftSectionDefinitionLinks(sections, fieldsList, "draft");
			var readyStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "ready");
			var inprogressStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "in_progress");
			var activeStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "active");
			var deactivateStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "deactivate");
			var deactivatingStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "deactivating");
			var reprovisionStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "reprovision");
			var completeStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "complete");

			return draftStatusLinks.Concat(readyStatusLinks).Concat(inprogressStatusLinks).Concat(activeStatusLinks).Concat(deactivateStatusLinks).Concat(deactivatingStatusLinks).Concat(reprovisionStatusLinks).Concat(completeStatusLinks).ToList();
		}

		private static List<IDomButtonDefinition> GetBehaviorButtons()
		{
			DomInstanceButtonDefinition provisionButton = new DomInstanceButtonDefinition("provision")
			{
				VisibilityCondition = new StatusCondition(new List<string> { "draft" }),
				ActionDefinitionIds = new List<string> { "provision" },
				Layout = new DomButtonDefinitionLayout { Text = "Provision" },
			};

			DomInstanceButtonDefinition deactivateButton = new DomInstanceButtonDefinition("deactivate")
			{
				VisibilityCondition = new StatusCondition(new List<string> { "active" }),
				ActionDefinitionIds = new List<string> { "deactivate" },
				Layout = new DomButtonDefinitionLayout { Text = "Deactivate" },
			};

			DomInstanceButtonDefinition reprovisionButton = new DomInstanceButtonDefinition("reprovision")
			{
				VisibilityCondition = new StatusCondition(new List<string> { "active" }),
				ActionDefinitionIds = new List<string> { "reprovision" },
				Layout = new DomButtonDefinitionLayout { Text = "Reprovision" },
			};

			DomInstanceButtonDefinition completeProvision = new DomInstanceButtonDefinition("complete-provision")
			{
				VisibilityCondition = new StatusCondition(new List<string> { "complete" }),
				ActionDefinitionIds = new List<string> { "complete-provision" },
				Layout = new DomButtonDefinitionLayout { Text = "Provision" },
			};

			List<IDomButtonDefinition> domButtons = new List<IDomButtonDefinition> { provisionButton, deactivateButton, reprovisionButton, completeProvision };
			return domButtons;
		}

		private static List<IDomActionDefinition> GetBehaviorActions(string processName, string businessKeyField)
		{
			var provisionAction = new ExecuteScriptDomActionDefinition("provision")
			{
				Script = "start_process",
				IsInteractive = false,
				ScriptOptions = new List<string>
				{
					$"PARAMETER:1:{processName}",
					"PARAMETER:2:draft_to_ready",
					$"PARAMETER:3:{businessKeyField}",
					"PARAMETER:4:provision",
				},
			};

			var deactivateAction = new ExecuteScriptDomActionDefinition("deactivate")
			{
				Script = "start_process",
				IsInteractive = false,
				ScriptOptions = new List<string>
				{
					$"PARAMETER:1:{processName}",
					"PARAMETER:2:active_to_deactivate",
					$"PARAMETER:3:{businessKeyField}",
					"PARAMETER:4:deactivate",
				},
			};

			var reprovisionAction = new ExecuteScriptDomActionDefinition("reprovision")
			{
				Script = "start_process",
				IsInteractive = false,
				ScriptOptions = new List<string>
				{
					$"PARAMETER:1:{processName}",
					"PARAMETER:2:active_to_reprovision",
					$"PARAMETER:3:{businessKeyField}",
					"PARAMETER:4:reprovision",
				},
			};

			var completeProvisionAction = new ExecuteScriptDomActionDefinition("complete-provision")
			{
				Script = "start_process",
				IsInteractive = false,
				ScriptOptions = new List<string>
				{
					$"PARAMETER:1:{processName}",
					"PARAMETER:2:complete_to_ready",
					$"PARAMETER:3:{businessKeyField}",
					"PARAMETER:4:complete-provision",
				},
			};

			var behaviorActions = new List<IDomActionDefinition> { provisionAction, deactivateAction, reprovisionAction, completeProvisionAction, };
			return behaviorActions;
		}

		private static Dictionary<string, List<FieldDescriptorID>> GetFieldDescriptorDictionary(List<SectionDefinition> sections)
		{
			Dictionary<string, List<FieldDescriptorID>> fieldsList = new Dictionary<string, List<FieldDescriptorID>>();
			foreach (var section in sections)
			{
				var fields = section.GetAllFieldDescriptors();
				foreach (var field in fields)
				{
					var sectionName = section.GetName();
					if (!fieldsList.ContainsKey(sectionName))
					{
						fieldsList[sectionName] = new List<FieldDescriptorID>();
					}

					fieldsList[sectionName].Add(field.ID);
				}
			}

			return fieldsList;
		}

		public class StatusSectionDefinitions
		{
			public static List<DomStatusSectionDefinitionLink> GetDraftSectionDefinitionLinks(List<SectionDefinition> sections, Dictionary<string, List<FieldDescriptorID>> fieldsList, string status)
			{
				var sectionLinks = new List<DomStatusSectionDefinitionLink>();

				var fieldDescriptors = sections.First(x => x.GetName().Contains("Provision Info")).GetAllFieldDescriptors().ToList();
				var requiredFieldDescriptorIDs = fieldDescriptors.FindAll(x => x.Name.Contains("Provision Name") || x.Name.Contains("Event ID")).Select(x => x.ID).ToList();

				foreach (var section in sections)
				{
					var statusLinkId = new DomStatusSectionDefinitionLinkId(status, section.GetID());

					var statusLink = new DomStatusSectionDefinitionLink(statusLinkId);

					foreach (var fieldId in fieldsList[section.GetName()])
					{
						statusLink.FieldDescriptorLinks.Add(new DomStatusFieldDescriptorLink(fieldId)
						{
							Visible = true,
							ReadOnly = false,
							RequiredForStatus = requiredFieldDescriptorIDs.Contains(fieldId),
						});
					}

					sectionLinks.Add(statusLink);
				}

				return sectionLinks;
			}

			public static List<DomStatusSectionDefinitionLink> GetSectionDefinitionLinks(List<SectionDefinition> sections, Dictionary<string, List<FieldDescriptorID>> fieldsList, string status)
			{
				var sectionLinks = new List<DomStatusSectionDefinitionLink>();
				var fieldDescriptors = sections.First(x => x.GetName().Contains("Provision Info")).GetAllFieldDescriptors().ToList();
				var writableFields = fieldDescriptors.FindAll(x => x.Name.Contains("InstanceId") || x.Name.Contains("Action")).Select(x => x.ID).ToList();

				foreach (var section in sections)
				{
					var statusLinkId = new DomStatusSectionDefinitionLinkId(status, section.GetID());

					var statusLink = new DomStatusSectionDefinitionLink(statusLinkId);

					foreach (var fieldId in fieldsList[section.GetName()])
					{
						statusLink.FieldDescriptorLinks.Add(new DomStatusFieldDescriptorLink(fieldId)
						{
							Visible = true,
							ReadOnly = !writableFields.Contains(fieldId),
							RequiredForStatus = false,
						});
					}

					sectionLinks.Add(statusLink);
				}

				return sectionLinks;
			}
		}
	}
}