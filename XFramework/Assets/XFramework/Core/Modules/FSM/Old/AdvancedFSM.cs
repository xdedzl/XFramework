using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace XFramework.OldFsm
{
    /// <summary>
    /// ״̬ת������
    /// </summary>
    public enum Transition
    {
        SawPlayer = 0,
        ReachPlayer,
        LostPlayer,
        NoHealth,
    }

    /// <summary>
    /// ״̬
    /// </summary>
    public enum FSMStateID
    {
        Patrolling = 0,
        Chasing,
        Attacking,
        Dead,
    }

    public class AdvancedFSM : FSM
    {
        private List<FSMState> fsmStates;

        //The fsmStates are not changing directly but updated by using transitions
        private FSMStateID currentStateID;
        public FSMStateID CurrentStateID { get { return currentStateID; } }

        private FSMState currentState;
        public FSMState CurrentState { get { return currentState; } }

        public AdvancedFSM()
        {
            fsmStates = new List<FSMState>();
        }

        /// <summary>
        /// Add New State into the list
        /// </summary>
        public void AddFSMState(FSMState fsmState)
        {
            // Check for Null reference before deleting
            if (fsmState == null)
            {
                Debug.LogError("FSM ERROR: Null reference is not allowed");
            }

            // First State inserted is also the Initial state
            //   the state the machine is in when the simulation begins
            if (fsmStates.Count == 0)
            {
                fsmStates.Add(fsmState);
                currentState = fsmState;
                currentStateID = fsmState.ID;
                return;
            }

            // Add the state to the List if it not inside it
            foreach (FSMState state in fsmStates)
            {
                if (state.ID == fsmState.ID)
                {
                    Debug.LogError("FSM ERROR: Trying to add a state that was already inside the list");
                    return;
                }
            }

            //If no state in the current then add the state to the list
            fsmStates.Add(fsmState);
        }


        //This method delete a state from the FSM List if it exists,     
        public void DeleteState(FSMStateID fsmState)
        {
            // Search the List and delete the state if it inside it
            foreach (FSMState state in fsmStates)
            {
                if (state.ID == fsmState)
                {
                    fsmStates.Remove(state);
                    return;
                }
            }
            Debug.LogError("FSM ERROR: The state passed was not on the list. Impossible to delete it");
        }

        /// <summary>
        /// This method tries to change the state the FSM is in based on
        /// the current state and the transition passed. 
        /// </summary>
        public void PerformTransition(Transition trans)
        {
            // Check if the currentState has the transition passed as argument
            FSMStateID id = currentState.GetOutputState(trans);

            // Update the currentStateID and currentState		
            currentStateID = id;
            foreach (FSMState state in fsmStates)
            {
                if (state.ID == currentStateID)
                {
                    currentState = state;
                    break;
                }
            }
        }
    }
}