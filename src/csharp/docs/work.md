# Go proxy

The proxy will be built in stages

1. Transport Interfact which will take care of the communication part
2. ClusterInforActor, which would take care of Sharing and updating cluster state
3. Gin based Api for interacting with activeFocusBorder

Api model. 

## route

    [post]
    - /api/v1/{clustername}/{actorname}/{actorId}/methodName
    headers
    {
        "requestType":""
        "correlationId":""
        ....
    }