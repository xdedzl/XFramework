using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace XFramework.OldFsm
{
    /// <summary>
    /// This class is adapted and modified from the FSM implementation class available on UnifyCommunity website
    /// The license for the code is Creative Commons Attribution Share Alike.
    /// It's originally the port of C++ FSM implementation mentioned in Chapter01 of Game Programming Gems 1
    /// You're free to use, modify and distribute the code in any projects including commercial ones.
    /// Please read the link to know more about CCA license @http://creativecommons.org/licenses/by-sa/3.0/

    /// This class represents the States in the Finite State System.
    /// Each state has a Dictionary with pairs (transition-state) showing
    /// which state the FSM should be if a transition is fired while this state
    /// is the current state.
    /// Reason method is used to determine which transition should be fired .
    /// Act method has the code to perform the actions the NPC is supposed to do if it´s on this state.
    /// </summary>
    public abstract class FSMState
    {
        protected Dictionary<Transition, FSMStateID> map = new Dictionary<Transition, FSMStateID>();
        protected FSMStateID stateID;
        public FSMStateID ID { get { return stateID; } }
        protected Vector3 destPos;
        protected Transform[] waypoints;
        protected float curRotSpeed;
        protected float curSpeed;
        protected float chaseDistance = 40.0f;
        protected float attackDistance = 20.0f;
        protected float arriveDistance = 3.0f;


        public void AddTransition(Transition transition, FSMStateID id)
        {
            //Since this is a Deterministc FSM,
            //Check if the current transition was already inside the map
            if (map.ContainsKey(transition))
            {
                Debug.LogWarning("FSMState ERROR: transition is already inside the map");
                return;
            }

            map.Add(transition, id);
            Debug.Log("Added : " + transition + " with ID : " + id);
        }

        /// <summary>
        /// This method deletes a pair transition-state from this state´s map.    
        /// </summary>
        public void DeleteTransition(Transition trans)
        {
            // Check if the pair is inside the map before deleting
            if (map.ContainsKey(trans))
            {
                map.Remove(trans);
                return;
            }
            Debug.LogError("FSMState ERROR: Transition passed was not on this State´s List");
        }


        /// <summary>
        /// This method returns the new state the FSM should be if
        ///    this state receives a transition  
        /// </summary>
        public FSMStateID GetOutputState(Transition trans)
        {
            return map[trans];
        }

        /// <summary>
        /// Decides if the state should transition to another on its list
        /// NPC is a reference to the npc tha is controlled by this class
        /// </summary>
        public abstract void Reason(Transform player, Transform npc);

        /// <summary>
        /// This method controls the behavior of the NPC in the game World.
        /// Every action, movement or communication the NPC does should be placed here
        /// NPC is a reference to the npc tha is controlled by this class
        /// </summary>
        public abstract void Act(Transform player, Transform npc);

        /// <summary>
        /// Find the next semi-random patrol point
        /// </summary>
        public void FindNextPoint()
        {
            //Debug.Log("Finding next point");
            int rndIndex = Random.Range(0, waypoints.Length);
            Vector3 rndPosition = Vector3.zero;
            destPos = waypoints[rndIndex].position + rndPosition;
        }

        /// <summary>
        /// Check whether the next random position is the same as current tank position
        /// </summary>
        /// <param name="pos">position to check</param>
        /*
        protected bool IsInCurrentRange(Transform trans, Vector3 pos)
        {
            float xPos = Mathf.Abs(pos.x - trans.position.x);
            float zPos = Mathf.Abs(pos.z - trans.position.z);

            if (xPos <= 50 && zPos <= 50)
                return true;

            return false;
        }*/
    }
}