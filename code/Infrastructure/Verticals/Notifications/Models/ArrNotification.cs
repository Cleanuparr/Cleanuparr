﻿using Data.Enums;

namespace Infrastructure.Verticals.Notifications.Models;

public record ArrNotification : Notification
{
    public required InstanceType InstanceType { get; init; }
    
    public required Uri InstanceUrl { get; init; }
    
    public required string Hash { get; init; }
    
    public Uri? Image { get; init; }
}