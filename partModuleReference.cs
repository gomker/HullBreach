using System;

/// <summary>
/// http://forum.kerbalspaceprogram.com/index.php?/topic/83583-official-partmodule-documentation/
/// </summary>
public class PartModuleReference
{
    public class ModuleTest : PartModule
    {
        /// <summary>
        /// Constructor style setup.
        /// Called in the Part\'s Awake method. 
        /// The model may not be built by this point.
        /// </summary>
        public override void OnAwake()
        {
        }

        /// <summary>
        /// Called during the Part startup.
        /// StartState gives flag values of initial state
        /// </summary>
        public override void OnStart(StartState state)
        {
        }

        /// <summary>
        /// Per-frame update
        /// Called ONLY when Part is ACTIVE!
        /// </summary>
        public override void OnUpdate()
        {
        }

        /// <summary>
        /// Per-physx-frame update
        /// Called ONLY when Part is ACTIVE!
        /// </summary>
        public override void OnFixedUpdate()
        {
        }

        /// <summary>
        /// Called when PartModule is asked to save its values.
        /// Can save additional data here.
        /// </summary>
        /// <param name='node'>The node to save in to</param>
        public override void OnSave(ConfigNode node)
        {
        }

        /// <summary>
        /// Called when PartModule is asked to load its values.
        /// Can load additional data here.
        /// </summary>
        /// <param name='node'>The node to load from</param>
        public override void OnLoad(ConfigNode node)
        {
        }
    }
}
