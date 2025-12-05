namespace I40Sharp.Messaging.Models;

/// <summary>
/// Alle unterstützten I4.0 Message Types gemäß Spezifikation
/// </summary>
public static class I40MessageTypes
{
    // Negotiation Types
    public const string CALL_FOR_PROPOSAL = "callForProposal";
    public const string PROPOSAL = "proposal";
    public const string ACCEPT_PROPOSAL = "acceptProposal";
    public const string DENY_PROPOSAL = "denyProposal";
    
    // Informational
    public const string INFORM = "inform";
    public const string INFORM_CONFIRM = "informConfirm";
    public const string FAILURE = "failure";
    public const string CONSENT = "consent";
    
    // Requirement-Oriented
    public const string REQUIREMENT = "requirement";
    public const string REQUIREMENT_INFORM = "requirementInform";
    public const string REQUIREMENT_REPEAT = "requirementRepeat";
    public const string REQUIREMENT_PREVIOUSLY = "requirementPreviously";
    public const string REQUIREMENT_TERMINATE = "requirementTerminate";
    
    // Lifecycle
    public const string LIFECYCLE_KILL_AGENT = "Lifecycle_killAgent";
    public const string LIFECYCLE_RESTART_AGENT = "Lifecycle_restartAgent";
    public const string LIFECYCLE_SPAWN_AGENT = "Lifecycle_spawnAgent";
    public const string LIFECYCLE_UPDATE_AGENT = "Lifecycle_updateAgent";
    
    // Order / Production Plan
    public const string RECIPE = "recipe";
    public const string ORDER_DELETE_ACTION = "Order_deleteAction";
    public const string ORDER_TERMINATE_ACTION = "Order_terminateAction";
    public const string ORDER_DONE_ACTION = "Order_doneAction";
    public const string ORDER_EXECUTE_ACTION = "Order_executeAction";
    public const string ORDER_PRODUCT_CREATION = "Order_productCreation";
}
