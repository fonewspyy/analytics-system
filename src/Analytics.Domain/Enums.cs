namespace Analytics.Domain;

// Member names map 1:1 to the PostgreSQL enum labels in snake_case
// (InProgress <-> 'in_progress'). Renaming a member is a schema change.

public enum UserRole { Admin, Manager, Member }

public enum ProjectStatus { Planning, Active, OnHold, Completed, Archived }

public enum TaskItemStatus { Todo, InProgress, Done, Cancelled }

public enum TaskPriority { Low, Medium, High, Critical }

public enum LogSeverity { Info, Warning, Error }
