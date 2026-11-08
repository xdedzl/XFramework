using System;

namespace XFramework.Interaction
{
    public class TargetInteractableAttribute : Attribute
    {
        public Type interactableType;
        
        public TargetInteractableAttribute(Type interactableType)
        {
            this.interactableType = interactableType;
        }
    }
    
    public interface IInteractionHandler
    {
        public void SetTarget(InteractionObject interactionObject);
        public InteractionObject GetTarget();
        public void SetVisible(bool visible);

        public void OnUpdate();
    }
    
    
    public abstract class InteractionHandlerBase<T>: IInteractionHandler where T : IInteractable
    {
        protected InteractionObject targetObject { get; private set; }
        protected T targetInteractable { get; private set; }
        
        public void SetTarget(InteractionObject interactionObject)
        {
            if (targetObject == interactionObject)
            {
                return;
            }
            
            if (interactionObject != null)
            {
                if (interactionObject?.interactable is T interactable)
                {
                    targetObject = interactionObject;
                    targetInteractable = interactable;
                }
                else
                {
                    throw new Exception("InteractionHandlerBase SetTarget failed: incompatible interactable type.");
                }
            }
            else
            {
                targetObject = null;
                targetInteractable = default;
            }
                
            OnTargetChange();

        }

        public InteractionObject GetTarget()
        {
            return targetObject;
        }

        public virtual void SetVisible(bool visible)
        {
        }

        public abstract void OnTargetChange();
        public abstract void OnUpdate();
    }
}
