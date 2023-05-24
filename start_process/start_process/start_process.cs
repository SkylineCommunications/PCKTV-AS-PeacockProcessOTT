namespace Script
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Permissions;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.MessageHandler;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Actions;
    using Skyline.DataMiner.Net.LogHelpers;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;
    using Skyline.DataMiner.Net.Sections;
    using Skyline.DataMiner.Net.Topology;
    using static Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager.PaManagers;

    public class Script
    {
        private DomHelper innerDomHelper;
        private Field actionField;

        /// <summary>
        /// The Script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(Engine engine)
        {
            engine.ExitFail("This script should be executed using the 'OnDomAction' entry point");
        }

        [AutomationEntryPoint(AutomationEntryPointType.Types.OnDomAction)]
        public void OnDomActionMethod(IEngine engine, ExecuteScriptDomActionContext context)
        {
            var instanceId = context.ContextId as DomInstanceId;
            innerDomHelper = new DomHelper(engine.SendSLNetMessages, instanceId.ModuleId);

            try
            {
                var process = engine.GetScriptParam("process").Value;
                var transition = engine.GetScriptParam("transition").Value;
                var keyField = engine.GetScriptParam("key").Value;
                var actionValue = engine.GetScriptParam("action").Value;

                string businessKey = GetBusinessKey(engine, keyField, instanceId, actionValue);

                if (!String.IsNullOrWhiteSpace(businessKey))
                {
                    innerDomHelper.DomInstances.DoStatusTransition(instanceId, transition);
                    UpdateAction(instanceId);
                    ProcessHelper.PushToken(process, businessKey, instanceId);
                }
            }
            catch (Exception ex)
            {
                engine.GenerateInformation("Exception starting process: " + ex);
            }
        }

        private void UpdateAction(DomInstanceId instanceId)
        {
            if (this.actionField == null)
            {
                return;
            }

            var dominstance = DomInstanceExposers.Id.Equal(instanceId);
            var instance = this.innerDomHelper.DomInstances.Read(dominstance).First();
            instance.AddOrUpdateFieldValue(this.actionField.SectionDefinition, this.actionField.FieldDescriptor, this.actionField.Value);
            this.innerDomHelper.DomInstances.Update(instance);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "Ignored")]
        private string GetBusinessKey(IEngine engine, string keyField, DomInstanceId instanceId, string actionValue)
        {
            string businessKey = "default";
            var dominstance = DomInstanceExposers.Id.Equal(instanceId);
            var instance = innerDomHelper.DomInstances.Read(dominstance).First();
            var instanceSet = false;
            var keyFound = false;

            SectionDefinition sectionToUpdate = null;
            FieldDescriptor fieldToUpdate = null;

            foreach (var section in instance.Sections)
            {
                Func<SectionDefinitionID, SectionDefinition> sectionDefinitionFunc = SetSectionDefinitionById;
                section.Stitch(sectionDefinitionFunc);

                foreach (var field in section.FieldValues)
                {
                    if (field.GetFieldDescriptor().Name.Contains("InstanceId"))
                    {
                        sectionToUpdate = section.GetSectionDefinition();
                        fieldToUpdate = field.GetFieldDescriptor();
                        instanceSet = true;
                    }

                    if (field.GetFieldDescriptor().Name.Contains("Action"))
                    {
                        this.actionField = new Field
                        {
                            SectionDefinition = section.GetSectionDefinition(),
                            FieldDescriptor = field.GetFieldDescriptor(),
                            Value = actionValue,
                        };
                    }

                    if (field.GetFieldDescriptor().Name.Contains(keyField))
                    {
                        businessKey = field.Value.ToString();
                        keyFound = true;
                    }

                    if (keyFound && instanceSet)
                    {
                        break;
                    }
                }

                if (keyFound && instanceSet)
                {
                    break;
                }
            }

            if (sectionToUpdate == null || fieldToUpdate == null)
            {
                engine.GenerateInformation("Failed to find section/field for updating InstanceId");
                return null;
            }

            instance.AddOrUpdateFieldValue(sectionToUpdate, fieldToUpdate, instanceId.Id.ToString());
            innerDomHelper.DomInstances.Update(instance);

            return businessKey;
        }

        private SectionDefinition SetSectionDefinitionById(SectionDefinitionID sectionDefinitionId)
        {
            return this.innerDomHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(sectionDefinitionId)).First();
        }

        public class Field
        {
            public SectionDefinition SectionDefinition { get; set; }

            public FieldDescriptor FieldDescriptor { get; set; }

            public string Value { get; set; }
        }
    }
}