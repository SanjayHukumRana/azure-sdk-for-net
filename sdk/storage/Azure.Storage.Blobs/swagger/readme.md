# Blob Storage
> see https://aka.ms/autorest

## Configuration
``` yaml
# Generate blob storage
input-file: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/storage-dataplane-preview/specification/storage/data-plane/Microsoft.BlobStorage/preview/2019-02-02/blob.json
output-folder: ../src/Generated
clear-output-folder: false

# Use the Azure C# Track 2 generator
# use: C:\src\Storage\Swagger\Generator
# We can't use relative paths here, so use a relative path in generate.ps1
azure-track2-csharp: true
```

## Customizations for Track 2 Generator
See the [AutoRest samples](https://github.com/Azure/autorest/tree/master/Samples/3b-custom-transformations)
for more about how we're customizing things.

### x-ms-code-generation-settings
``` yaml
directive:
- from: swagger-document
  where: $.info["x-ms-code-generation-settings"]
  transform: >
    $.namespace = "Azure.Storage.Blobs";
    $["client-name"] = "BlobRestClient";
    $["client-extensions-name"] = "BlobsExtensions";
    $["client-model-factory-name"] = "BlobsModelFactory";
    $["x-az-skip-path-components"] = true;
    $["x-az-include-sync-methods"] = true;
    $["x-az-public"] = false;
```

### Move directory operations last
This is purely cosmetic for a cleaner diff.
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]
  transform: >
    const directoryPaths = [];
    for (var path in $) {
        const op = Object.values($[path])[0];
        if (op.operationId.startsWith("Directory_")) {
            directoryPaths.push(path);
        }
    }
    for (var path of directoryPaths) {
        const ops = $[path];
        delete $[path];
        $[path] = ops;
    }
```

### Remove extra consumes/produces values
To avoid an arbitrary limitation in our generator
``` yaml
directive:
- from: swagger-document
  where: $.consumes
  transform: >
    return ["application/xml"];
- from: swagger-document
  where: $.produces
  transform: >
    return ["application/xml"];
```

### Url
``` yaml
directive:
- from: swagger-document
  where: $.parameters.Url
  transform: >
    $["x-ms-client-name"] = "resourceUri";
    $.format = "url";
    $["x-az-trace"] = true;
```

### Ignore common headers
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]..responses..headers["x-ms-request-id"]
  transform: >
    $["x-az-demote-header"] = true;
- from: swagger-document
  where: $["x-ms-paths"]..responses..headers["x-ms-version"]
  transform: >
    $["x-az-demote-header"] = true;
- from: swagger-document
  where: $["x-ms-paths"]..responses..headers["Date"]
  transform: >
    $["x-az-demote-header"] = true;
- from: swagger-document
  where: $["x-ms-paths"]..responses..headers["x-ms-client-request-id"]
  transform: >
    $["x-az-demote-header"] = true;
```

### Clean up Failure response
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]..responses.default
  transform: >
    delete $.headers;
    $["x-az-response-name"] = "StorageErrorResult";
    $["x-az-create-exception"] = true;
    $["x-az-public"] = false;
```

### /?restype=service&comp=properties
``` yaml
directive:
- from: swagger-document
  where: $.definitions
  transform: >
    if (!$.BlobServiceProperties) {
        $.BlobServiceProperties = $.StorageServiceProperties;
        delete $.StorageServiceProperties;
        $.BlobServiceProperties.xml = { "name": "StorageServiceProperties" };
    }
    if (!$.BlobContainerProperties) {
        $.BlobContainerProperties = $.ContainerProperties;
        delete $.ContainerProperties;
    }
    if (!$.BlobContainerItem) {
        $.BlobContainerItem = $.ContainerItem;
        const path = $.BlobContainerItem.properties.Properties.$ref.replace(/[#].*$/, "#/definitions/BlobContainerProperties");
        $.BlobContainerItem.properties.Properties.$ref = path;
        delete $.ContainerItem;
    }
- from: swagger-document
  where: $.parameters
  transform: >
    if (!$.BlobServiceProperties) {
        const props = $.BlobServiceProperties = $.StorageServiceProperties;
        props.name = "BlobServiceProperties";
        props.schema = { "$ref": props.schema.$ref.replace(/[#].*$/, "#/definitions/BlobServiceProperties") };
        delete $.StorageServiceProperties;
    }
- from: swagger-document
  where: $["x-ms-paths"]["/?restype=service&comp=properties"]
  transform: >
    const param = $.put.parameters[0];
    if (param && param["$ref"] && param["$ref"].endsWith("StorageServiceProperties")) {
        const path = param["$ref"].replace(/[#].*$/, "#/parameters/BlobServiceProperties");
        $.put.parameters[0] = { "$ref": path };
    }
    const def = $.get.responses["200"].schema;
    if (def && def["$ref"] && def["$ref"].endsWith("StorageServiceProperties")) {
        const path = def["$ref"].replace(/[#].*$/, "#/definitions/BlobServiceProperties");
        $.get.responses["200"].schema = { "$ref": path };
    }
```

### Make CORS allow null values
It should be possible to pass null for CORS to update service properties without changing existing rules.
``` yaml
directive:
- from: swagger-document
  where: $.definitions.BlobServiceProperties
  transform: >
    $.properties.Cors["x-az-nullable-array"] = true;
```

### /?restype=service&comp=stats
``` yaml
directive:
- from: swagger-document
  where: $.definitions
  transform: >
    if (!$.BlobServiceStatistics) {
        $.BlobServiceStatistics = $.StorageServiceStats;
        delete $.StorageServiceStats;
        $.BlobServiceStatistics.xml = { "name": "StorageServiceStats" };
        $.BlobServiceStatistics.description = "Statistics for the storage service.";
    }
- from: swagger-document
  where: $["x-ms-paths"]["/?restype=service&comp=stats"]
  transform: >
    const def = $.get.responses["200"].schema;
    if (def && def["$ref"] && !def["$ref"].endsWith("BlobServiceStatistics")) {
        const path = def["$ref"].replace(/[#].*$/, "#/definitions/BlobServiceStatistics");
        $.get.responses["200"].schema = { "$ref": path };
    }
```

### /?comp=list
``` yaml
directive:
- from: swagger-document
  where: $.definitions
  transform: >
    if (!$.BlobContainersSegment) {
        $.BlobContainersSegment = $.ListContainersSegmentResponse;
        delete $.ListContainersSegmentResponse;
        $.BlobContainersSegment["x-az-public"] = false;
        $.BlobContainersSegment.required.push("NextMarker");
        $.BlobContainersSegment.properties.BlobContainerItems = $.BlobContainersSegment.properties.ContainerItems;
        delete $.BlobContainersSegment.properties.ContainerItems;
        const path = $.BlobContainersSegment.properties.BlobContainerItems.items.$ref.replace(/[#].*$/, "#/definitions/BlobContainerItem");
        $.BlobContainersSegment.properties.BlobContainerItems.items.$ref = path;
    }
- from: swagger-document
  where: $["x-ms-paths"]["/?comp=list"]
  transform: >
    const def = $.get.responses["200"].schema;
    if (def && def["$ref"] && !def["$ref"].endsWith("BlobContainersSegment")) {
        const path = def["$ref"].replace(/[#].*$/, "#/definitions/BlobContainersSegment");
        $.get.responses["200"].schema = { "$ref": path };
    }
    $.get.operationId = "Service_ListBlobContainersSegment";
```

### /{containerName}?restype=container
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?restype=container"]
  transform: >
    $.get.responses["200"].headers["x-ms-lease-state"]["x-ms-enum"].name = "LeaseState";
    $.get.responses["200"].headers["x-ms-lease-status"]["x-ms-enum"].name = "LeaseStatus";
    $.get.responses["200"].headers["x-ms-blob-public-access"]["x-ms-enum"].modelAsString = false;
    $.get.responses["200"]["x-az-response-name"] = "FlattenedContainerItem";
    $.get.responses["200"]["x-az-public"] = false;
    $.put.responses["201"]["x-az-response-name"] = "BlobContainerInfo";

```

### BlobPublicAccess
``` yaml
directive:
- from: swagger-document
  where: $.parameters.BlobPublicAccess
  transform: >
    $["x-ms-enum"].modelAsString = false;
```

### /?restype=account&comp=properties
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/?restype=account&comp=properties"]
  transform: >
    $.get.description = "Returns the sku name and account kind";
    $.get.responses["200"]["x-az-response-name"] = "AccountInfo";
- from: swagger-document
  where: $["x-ms-paths"]
  transform: >
    if ($["/{containerName}?restype=account&comp=properties"]) {
        delete $["/{containerName}?restype=account&comp=properties"];
    }
    if ($["/{containerName}/{blob}?restype=account&comp=properties"]) {
        delete $["/{containerName}/{blob}?restype=account&comp=properties"];
    }
```

### /{containerName}?restype=container&comp=metadata
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?restype=container&comp=metadata"]
  transform: >
    $.put.responses["200"]["x-az-response-name"] = "BlobContainerInfo";
```

### /{containerName}?restype=container&comp=acl
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?restype=container&comp=acl"]
  transform: >
    $.get.responses["200"].headers["x-ms-blob-public-access"]["x-ms-enum"].modelAsString = false;
    $.get.responses["200"]["x-az-response-name"] = "BlobContainerAccessPolicy";
    $.get.responses["200"]["x-az-response-schema-name"] = "SignedIdentifiers";
    $.put.responses["200"].description = "Success";
    $.put.responses["200"]["x-az-response-name"] = "BlobContainerInfo";
- from: swagger-document
  where: $.parameters.ContainerAcl
  transform: $["x-ms-client-name"] = "permissions"
- from: swagger-document
  where: $.definitions.SignedIdentifier
  transform: >
    delete $.xml;
```

### /{containerName}?comp=lease&restype=container&acquire
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?comp=lease&restype=container&acquire"]
  transform: >
    $.put.responses["201"].description = "The lease operation completed successfully.";
    $.put.responses["201"].headers["x-ms-lease-id"].description = "Uniquely identifies a container's or blob's lease";
    $.put.responses["201"]["x-az-response-name"] = "Lease";
```

### /{containerName}?comp=lease&restype=container&release
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?comp=lease&restype=container&release"]
  transform: >
    $.put.responses["200"].description = "Success";
    $.put.responses["200"]["x-az-response-name"] = "BlobContainerInfo";
```

### /{containerName}?comp=lease&restype=container&renew
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?comp=lease&restype=container&renew"]
  transform: >
    $.put.responses["200"].description = "The lease operation completed successfully.";
    $.put.responses["200"].headers["x-ms-lease-id"].description = "Uniquely identifies a container's or blob's lease";
    $.put.responses["200"]["x-az-response-name"] = "Lease";
```

### /{containerName}?comp=lease&restype=container&break
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?comp=lease&restype=container&break"]
  transform: >
    $.put.responses["202"]["x-az-response-name"] = "BrokenLease";
    $.put.responses["202"]["x-az-public"] = false;
```

### /{containerName}?comp=lease&restype=container&change
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?comp=lease&restype=container&change"]
  transform: >
    $.put.responses["200"].description = "The lease operation completed successfully.";
    $.put.responses["200"].headers["x-ms-lease-id"].description = "Uniquely identifies a container's or blob's lease";
    $.put.responses["200"]["x-az-response-name"] = "Lease";
```

### /{containerName}?restype=container&comp=list&flat
``` yaml
directive:
- from: swagger-document
  where: $.definitions
  transform: >
    if (!$.BlobsFlatSegment) {
        $.BlobsFlatSegment = $.ListBlobsFlatSegmentResponse;
        delete $.ListBlobsFlatSegmentResponse;
        $.BlobsFlatSegment["x-ms-client-name"] = "BlobsFlatSegment";
        $.BlobsFlatSegment["x-az-public"] = false;
        $.BlobsFlatSegment.required.push("NextMarker");
        const path = $.BlobsFlatSegment.properties.Segment.$ref.replace(/[#].*$/, "#/definitions/");
        $.BlobsFlatSegment.properties.BlobItems = {
            "type": "array",
            "xml": { "name": "Blobs", "wrapped": true },
            "items": { "$ref": path + "BlobItem" }
        };
        delete $.BlobsFlatSegment.properties.Segment;
        delete $.BlobFlatListSegment;
    }
    $.BlobItem.required = [ "Name", "Properties" ];
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?restype=container&comp=list&flat"]
  transform: >
    $.get.operationId = "Container_ListBlobsFlatSegment";
    $.get.responses["200"].headers["Content-Type"]["x-az-demote-header"] = true;
    const def = $.get.responses["200"].schema;
    if (def && def["$ref"] && !def["$ref"].endsWith("BlobsFlatSegment")) {
        const path = def["$ref"].replace(/[#].*$/, "#/definitions/BlobsFlatSegment");
        $.get.responses["200"].schema = { "$ref": path };
    }
```

### /{containerName}?restype=container&comp=list&hierarchy
``` yaml
directive:
- from: swagger-document
  where: $.definitions
  transform: >
    if (!$.BlobsHierarchySegment) {
        $.BlobsHierarchySegment = $.ListBlobsHierarchySegmentResponse;
        delete $.ListBlobsHierarchySegmentResponse;
        $.BlobsHierarchySegment["x-ms-client-name"] = "BlobsHierarchySegment";
        $.BlobsHierarchySegment["x-az-public"] = false;
        $.BlobsHierarchySegment.required.push("NextMarker");
        const path = $.BlobsHierarchySegment.properties.Segment.$ref.replace(/[#].*$/, "#/definitions/");
        $.BlobsHierarchySegment.properties.BlobItems = {
            "type": "array",
            "xml": { "name": "Blobs", "wrapped": true },
            "items": { "$ref": path + "BlobItem" }
        };
        $.BlobsHierarchySegment.properties.BlobPrefixes = {
            "type": "array",
            "xml": { "name": "Blobs", "wrapped": true },
            "items": { "$ref": path + "BlobPrefix" }
        };
        delete $.BlobsHierarchySegment.properties.Segment;
        delete $.BlobHierarchyListSegment;
    }
    $.BlobPrefix["x-az-public"] = false;
- from: swagger-document
  where: $.parameters.Delimiter
  transform: >
    $.required = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}?restype=container&comp=list&hierarchy"]
  transform: >
    $.get.operationId = "Container_ListBlobsHierarchySegment";
    $.get.responses["200"].headers["Content-Type"]["x-az-demote-header"] = true;
    const def = $.get.responses["200"].schema;
    if (def && def["$ref"] && !def["$ref"].endsWith("BlobsHierarchySegment")) {
        const path = def["$ref"].replace(/[#].*$/, "#/definitions/BlobsHierarchySegment");
        $.get.responses["200"].schema = { "$ref": path };
    }
```

### MD5 to Hash
``` yaml
directive:
- from: swagger-document
  where: $.parameters.BlobContentMD5
  transform: >
    $["x-ms-client-name"] = "blobContentHash";
- from: swagger-document
  where: $.parameters.ContentMD5
  transform: >
    $["x-ms-client-name"] = "transactionalContentHash";
- from: swagger-document
  where: $.parameters.GetRangeContentMD5
  transform: >
    $["x-ms-client-name"] = "rangeGetContentHash";
- from: swagger-document
  where: $.parameters.SourceContentMD5
  transform: >
    $["x-ms-client-name"] = "sourceContentHash";
- from: swagger-document
  where: $["x-ms-paths"]..responses..headers["Content-MD5"]
  transform: >
    $["x-ms-client-name"] = "ContentHash";
```

### /{containerName}/{blob}
``` yaml
directive:
- from: swagger-document
  where: $.definitions
  transform: >
    if (!$.BlobItemProperties) {
        $.BlobItemProperties = $.BlobProperties;
        delete $.BlobProperties;
        $.BlobItemProperties.properties.ETag = $.BlobItemProperties.properties.Etag;
        $.BlobItemProperties.properties.ETag.xml = { "name":  "Etag" };
        delete $.BlobItemProperties.properties.Etag;
        delete $.BlobItemProperties.required;
        $.BlobItemProperties.properties["Content-MD5"]["x-ms-client-name"] = "ContentHash";
        $.BlobItemProperties.properties.CopySource.format = "url";
        const path = $.BlobItem.properties.Properties.$ref.replace(/[#].*$/, "#/definitions/BlobItemProperties");
        $.BlobItem.properties.Properties = { "$ref": path };
    }
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}"]
  transform: >
    $.get.responses["200"]["x-az-response-name"] = "FlattenedDownloadProperties";
    $.get.responses["200"]["x-az-public"] = false;
    $.get.responses["200"]["x-az-stream"] = true;
    $.get.responses["200"].headers["x-ms-copy-source"].format = "url";
    $.get.responses["200"].headers["x-ms-copy-status"]["x-ms-enum"].name = "CopyStatus";
    $.get.responses["200"].headers["x-ms-lease-state"]["x-ms-enum"].name = "LeaseState";
    $.get.responses["200"].headers["x-ms-lease-status"]["x-ms-enum"].name = "LeaseStatus";
    $.get.responses["200"].headers["x-ms-blob-content-md5"]["x-ms-client-name"] = "BlobContentHash";
    $.get.responses["200"]["x-az-response-schema-name"] = "Content";
    $.get.responses["206"]["x-az-response-name"] = "FlattenedDownloadProperties";
    $.get.responses["206"]["x-az-public"] = false;
    $.get.responses["206"]["x-az-stream"] = true;
    $.get.responses["206"].headers["x-ms-copy-source"].format = "url";
    $.get.responses["206"].headers["x-ms-copy-status"]["x-ms-enum"].name = "CopyStatus";
    $.get.responses["206"].headers["x-ms-lease-state"]["x-ms-enum"].name = "LeaseState";
    $.get.responses["206"].headers["x-ms-lease-status"]["x-ms-enum"].name = "LeaseStatus";
    $.get.responses["206"].headers["x-ms-blob-content-md5"]["x-ms-client-name"] = "BlobContentHash";
    $.get.responses["206"]["x-az-response-schema-name"] = "Content";
    $.get.responses["304"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } } };
    $.head.responses["200"]["x-az-response-name"] = "BlobProperties";
    $.head.responses["200"].headers["x-ms-copy-source"].format = "url";
    $.head.responses["200"].headers["x-ms-copy-status"]["x-ms-enum"].name = "CopyStatus";
    $.head.responses["200"].headers["x-ms-lease-state"]["x-ms-enum"].name = "LeaseState";
    $.head.responses["200"].headers["x-ms-lease-status"]["x-ms-enum"].name = "LeaseStatus";
    $.head.responses["200"].headers["Content-MD5"]["x-ms-client-name"] = "ContentHash";
    $.head.responses["200"].headers["Content-Encoding"].type = "array";
    $.head.responses["200"].headers["Content-Encoding"].collectionFormat = "csv";
    $.head.responses["200"].headers["Content-Encoding"].items = { "type": "string" };
    $.head.responses["200"].headers["Content-Language"].type = "array";
    $.head.responses["200"].headers["Content-Language"].collectionFormat = "csv";
    $.head.responses["200"].headers["Content-Language"].items = { "type": "string" };
    $.head.responses["304"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } }
    };
    $.head.responses["default"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } }
    };
```

### Remove Type suffix from enum names
``` yaml
directive:
- from: swagger-document
  where: $.parameters.DeleteSnapshots
  transform: >
    $["x-ms-enum"].name = "DeleteSnapshotsOption";
- from: swagger-document
  where: $.parameters.SequenceNumberAction
  transform: >
    $["x-ms-enum"].name = "SequenceNumberAction";
- from: swagger-document
  where: $.definitions.CopyStatus
  transform: >
    $["x-ms-enum"].name = "CopyStatus";
- from: swagger-document
  where: $.definitions.LeaseState
  transform: >
    $["x-ms-enum"].name = "LeaseState";
- from: swagger-document
  where: $.definitions.LeaseStatus
  transform: >
    $["x-ms-enum"].name = "LeaseStatus";
- from: swagger-document
  where: $.definitions.GeoReplication.properties.Status
  transform: >
    $["x-ms-enum"].name = "GeoReplicationStatus";
    $["x-ms-enum"].modelAsString = false;
```

### /{containerName}/{blob}?comp=properties&SetHTTPHeaders
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=properties&SetHTTPHeaders"]
  transform: >
    $.put.operationId = "Blob_SetHttpHeaders";
    $.put.responses["200"]["x-az-response-name"] = "SetHttpHeadersOperation";
    $.put.responses["200"]["x-az-public"] = false;
- from: swagger-document
  where: $.parameters.BlobContentEncoding
  transform: >
    $.type = "array";
    $.collectionFormat = "csv";
    $.items = { "type": "string" };
- from: swagger-document
  where: $.parameters.BlobContentLanguage
  transform: >
    $.type = "array";
    $.collectionFormat = "csv";
    $.items = { "type": "string" };
```

### /{containerName}/{blob}?comp=metadata
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=metadata"]
  transform: >
    $.put.responses["200"]["x-az-response-name"] = "SetMetadataOperation";
    $.put.responses["200"]["x-az-public"] = false;
```

### /{containerName}/{blob}?comp=lease&acquire
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=lease&acquire"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "Lease";
    $.put.responses["201"].description = "The lease operation completed successfully.";
    $.put.responses["201"].headers["x-ms-lease-id"].description = "Uniquely identifies a container's or blob's lease";
```

### /{containerName}/{blob}?comp=lease&release
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=lease&release"]
  transform: >
    $.put.responses["200"].description = "The operation completed successfully.";
    $.put.responses["200"]["x-az-response-name"] = "BlobInfo";
```

### /{containerName}/{blob}?comp=lease&renew
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=lease&renew"]
  transform: >
    $.put.responses["200"]["x-az-response-name"] = "Lease";
    $.put.responses["200"].description = "The lease operation completed successfully.";
    $.put.responses["200"].headers["x-ms-lease-id"].description = "Uniquely identifies a container's or blob's lease";
```

### /{containerName}/{blob}?comp=lease&change
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=lease&change"]
  transform: >
    $.put.responses["200"]["x-az-response-name"] = "Lease";
    $.put.responses["200"].description = "The lease operation completed successfully.";
    $.put.responses["200"].headers["x-ms-lease-id"].description = "Uniquely identifies a container's or blob's lease";
```

### /{containerName}/{blob}?comp=lease&break
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=lease&break"]
  transform: >
    $.put.responses["202"]["x-az-response-name"] = "BrokenLease";
    $.put.responses["202"]["x-az-public"] = false;
```

### /{containerName}/{blob}?comp=snapshot
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=snapshot"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "BlobSnapshotInfo";
```

### /{containerName}/{blob}?comp=copy
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=copy"]
  transform: >
    $.put.operationId = "Blob_StartCopyFromUri";
    $.put.responses["202"]["x-az-response-name"] = "BlobCopyInfo";
    $.put.responses["202"].description = "The operation completed successfully.";
    $.put.responses["202"].headers["x-ms-copy-status"]["x-ms-enum"].name = "CopyStatus";
```

### /{containerName}/{blob}?comp=copy&sync
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=copy&sync"]
  transform: >
    $.put.operationId = "Blob_CopyFromUri";
    $.put.responses["202"]["x-az-response-name"] = "BlobCopyInfo";
    $.put.responses["202"].description = "The operation completed successfully.";
    $.put.responses["202"].headers["x-ms-copy-status"].enum = ["pending", "success", "aborted", "failed"];
    $.put.responses["202"].headers["x-ms-copy-status"]["x-ms-enum"].name = "CopyStatus";
```

### /{containerName}/{blob}?comp=copy&copyid={CopyId}
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=copy&copyid={CopyId}"]
  transform: >
    $.put.operationId = "Blob_AbortCopyFromUri";
```

### SourceUrl
``` yaml
directive:
- from: swagger-document
  where: $.parameters.SourceUrl
  transform: >
    $["x-ms-client-name"] = "sourceUri"
```

### /{containerName}/{blob}?PageBlob
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?PageBlob"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "BlobContentInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
    $.put.responses["201"].headers["x-ms-blob-sequence-number"] = {
        "x-ms-client-name": "BlobSequenceNumber",
        "type": "integer",
        "format": "int64",
        "description": "The current sequence number for the page blob.  This is only returned for page blobs."
    };
```

### /{containerName}/{blob}?comp=page&update
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=page&update"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "PageInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-blob-sequence-number"].description = "The current sequence number for the page blob.  This is only returned for page blobs.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
```

### /{containerName}/{blob}?comp=page&clear
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=page&clear"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "PageInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-blob-sequence-number"].description = "The current sequence number for the page blob.  This is only returned for page blobs.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"] = {
        "x-ms-client-name": "IsServerEncrypted",
        "type": "boolean",
        "x-az-demote-header": true,
        "description": "The value of this header is set to true if the contents of the request are successfully encrypted using the specified algorithm, and false otherwise."
    };
```

### /{containerName}/{blob}?comp=page&update&fromUrl
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=page&update&fromUrl"]
  transform: >
    $.put.operationId = "PageBlob_UploadPagesFromUri";
    $.put.responses["201"]["x-az-response-name"] = "PageInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-blob-sequence-number"].description = "The current sequence number for the page blob.  This is only returned for page blobs.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
    $.put.responses["304"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } }
    };
```

### /{containerName}/{blob}?comp=pagelist
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=pagelist"]
  transform: >
    $.get.responses["200"]["x-az-response-name"] = "PageRangesInfo";
    $.get.responses["304"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } }
    };
```

### /{containerName}/{blob}?comp=pagelist&diff
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=pagelist&diff"]
  transform: >
    $.get.responses["200"]["x-az-response-name"] = "PageRangesInfo";
    $.get.responses["304"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } }
    };
```

### /{containerName}/{blob}?comp=properties&Resize
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=properties&Resize"]
  transform: >
    $.put.responses["200"]["x-az-response-name"] = "PageBlobInfo";
    $.put.responses["200"].description = "The operation completed successfully.";
    $.put.responses["200"].headers["Last-Modified"].description = "Returns the date and time the blob was last modified. Any operation that modifies the blob, including an update of the blob's metadata or properties, changes the last-modified time of the blob.";
    $.put.responses["200"].headers["x-ms-blob-sequence-number"].description = "The current sequence number for the page blob.  This is only returned for page blobs.";
```

### /{containerName}/{blob}?comp=properties&UpdateSequenceNumber
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=properties&UpdateSequenceNumber"]
  transform: >
    $.put.responses["200"]["x-az-response-name"] = "PageBlobInfo";
    $.put.responses["200"].description = "The operation completed successfully.";
    $.put.responses["200"].headers["Last-Modified"].description = "Returns the date and time the blob was last modified. Any operation that modifies the blob, including an update of the blob's metadata or properties, changes the last-modified time of the blob.";
    $.put.responses["200"].headers["x-ms-blob-sequence-number"].description = "The current sequence number for the page blob.  This is only returned for page blobs.";
```

### /{containerName}/{blob}?comp=incrementalcopy
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=incrementalcopy"]
  transform: >
    $.put.responses["202"]["x-az-response-name"] = "BlobCopyInfo";
    $.put.responses["202"].description = "The operation completed successfully.";
    $.put.responses["202"].headers["x-ms-copy-status"]["x-ms-enum"].name = "CopyStatus";
```

### /{containerName}/{blob}?AppendBlob
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?AppendBlob"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "BlobContentInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
    $.put.responses["201"].headers["x-ms-blob-sequence-number"] = {
        "x-ms-client-name": "BlobSequenceNumber",
        "type": "integer",
        "format": "int64",
        "description": "The current sequence number for the page blob.  This is only returned for page blobs."
    };
```

### /{containerName}/{blob}?comp=appendblock
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=appendblock"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "BlobAppendInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
```

### /{containerName}/{blob}?comp=appendblock&fromUrl
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=appendblock&fromUrl"]
  transform: >
    $.put.operationId = "AppendBlob_AppendBlockFromUri";
    $.put.responses["201"]["x-az-response-name"] = "BlobAppendInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"] = {
        "x-ms-client-name": "IsServerEncrypted",
        "type": "boolean",
        "description": "The value of this header is set to true if the contents of the request are successfully encrypted using the specified algorithm, and false otherwise."
    };
    $.put.responses["304"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } }
    };
```

### /{containerName}/{blob}?BlockBlob
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?BlockBlob"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "BlobContentInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
    $.put.responses["201"].headers["x-ms-blob-sequence-number"] = {
        "x-ms-client-name": "BlobSequenceNumber",
        "type": "integer",
        "format": "int64",
        "description": "The current sequence number for the page blob.  This is only returned for page blobs."
    };
```

### /{containerName}/{blob}?comp=block
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=block"]
  transform: >
    $.put.responses["201"]["x-az-response-name"] = "BlockInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
```

### /{containerName}/{blob}?comp=block&fromURL
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=block&fromURL"]
  transform: >
    $.put.operationId = "BlockBlob_StageBlockFromUri";
    $.put.responses["201"]["x-az-response-name"] = "BlockInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
    $.put.responses["304"] = {
        "description": "The condition specified using HTTP conditional header(s) is not met.",
        "x-az-response-name": "ConditionNotMetError",
        "x-az-create-exception": true,
        "x-az-public": false,
        "headers": { "x-ms-error-code": { "x-ms-client-name": "ErrorCode", "type": "string" } }
    };
```

### /{containerName}/{blob}?comp=blocklist
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=blocklist"]
  transform: >
    $.get.responses["200"]["x-az-response-name"] = "GetBlockListOperation";
    $.get.responses["200"]["x-az-public"] = false;
    $.put.responses["201"]["x-az-response-name"] = "BlobContentInfo";
    $.put.responses["201"].description = "The operation completed successfully.";
    $.put.responses["201"].headers["x-ms-request-server-encrypted"]["x-az-demote-header"] = true;
    $.put.responses["201"].headers["x-ms-blob-sequence-number"] = {
        "x-ms-client-name": "BlobSequenceNumber",
        "type": "integer",
        "format": "int64",
        "description": "The current sequence number for the page blob.  This is only returned for page blobs."
    };
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-az-demote-header"] = true;
```

### BlockLookupList
``` yaml
directive:
- from: swagger-document
  where: $.definitions.BlockLookupList
  transform: >
    $["x-az-public"] = false;
    $.description = "A list of block IDs split between the committed block list, in the uncommitted block list, or in the uncommitted block list first and then in the committed block list.";
```

### ErrorCode
``` yaml
directive:
- from: swagger-document
  where: $.definitions.ErrorCode
  transform: >
    $["x-ms-enum"].name = "BlobErrorCode";
```

### ContainerProperties
``` yaml
directive:
- from: swagger-document
  where: $.definitions.BlobContainerProperties
  transform: >
    $.required = ["Last-Modified", "ETag"];
    $.properties.ETag = $.properties.Etag;
    $.properties.ETag.xml = { "name":  "Etag" };
    delete $.properties.Etag;
```

### Make sure everything has a type
``` yaml
directive:
- from: swagger-document
  where: $.definitions.Metrics
  transform: >
    $.type = "object";
- from: swagger-document
  where: $.definitions.BlobTags
  transform: >
    $.type = "object";
- from: swagger-document
  where: $.definitions.DataLakeStorageError
  transform: >
    $.properties.error.type = "object";
```

### KeyInfo
``` yaml
directive:
- from: swagger-document
  where: $.definitions.KeyInfo
  transform: >
    $.required = ["Expiry"];
    $.properties.Start.format = $.properties.Expiry.format = "date-time-8601";
    $["x-az-public"] = false;
```

### Hide various Include types
``` yaml
directive:
- from: swagger-document
  where: $.parameters.ListBlobsInclude
  transform: >
    $.items["x-az-public"] = false;
- from: swagger-document
  where: $.parameters.ListContainersInclude
  transform: >
    $["x-az-public"] = false;
    $["x-ms-enum"].name = "ListBlobContainersIncludeType"
```

### Logging
``` yaml
directive:
- from: swagger-document
  where: $.definitions.Logging
  transform: >
    $["x-az-disable-warnings"] = "CA1724";
```

### Hide Error models
``` yaml
directive:
- from: swagger-document
  where: $.definitions.StorageError
  transform: >
    $["x-az-public"] = false;
    $.properties.Code = { "type": "string" };
- from: swagger-document
  where: $.definitions.DataLakeStorageError
  transform: >
    $["x-az-public"] = false;
- from: swagger-document
  where: $.definitions.DataLakeStorageError.properties["error"]
  transform: >
    $["x-az-public"] = false;
```

### Fix doc comments
``` yaml
directive:
- from: swagger-document
  where: $.parameters.BlobTagsHeader
  transform: >
    $.description = "Optional. A URL encoded query param string which specifies the tags to be created with the Blob object. e.g. TagName1=TagValue1&amp;TagName2=TagValue2. The x-ms-tags header may contain up to 2kb of tags.";
- from: swagger-document
  where: $["x-ms-paths"]..get.responses..headers["x-ms-content-crc64"]
  transform: >
    $.description = "If the request is to read a specified range and the x-ms-range-get-content-crc64 is set to true, then the request returns a crc64 for the range, as long as the range size is less than or equal to 4 MB. If both x-ms-range-get-content-crc64 and x-ms-range-get-content-md5 is specified in the same request, it will fail with 400(Bad Request)";
- from: swagger-document
  where: $["x-ms-paths"]["/?comp=batch"].get.responses["200"].headers["Content-Type"]
  transform: >
    $.description = $.description.replace("<GUID>", "{GUID}");
- from: swagger-document
  where: $.parameters.MultipartContentType
  transform: >
    $.description = $.description.replace("<GUID>", "{GUID}");
```

### Add PublicAccessType.None
``` yaml
directive:
- from: swagger-document
  where:
    - $.definitions.PublicAccessType
    - $.parameters.BlobPublicAccess
    - $["x-ms-paths"]["/{containerName}?restype=container"].get.responses["200"].headers["x-ms-blob-public-access"]
    - $["x-ms-paths"]["/{containerName}?restype=container&comp=acl"].get.responses["200"].headers["x-ms-blob-public-access"]
  transform: >
    $.enum = [ "none", "container", "blob" ];
    $["x-ms-enum"].values = [ { name: "none", value: null }, { name: "blobContainer", value: "container" }, { name: "blob", value: "blob" }];
    $["x-az-enum-skip-value"] = "none";
- from: swagger-document
  where: $.definitions.PublicAccessType
  transform: >
    $["x-ms-enum"].modelAsString = false;
- from: swagger-document
  where: $.parameters.BlobPublicAccess
  transform: $.required = true;
- from: swagger-document
  where: $.definitions.ContainerProperties
  transform: $.required.push("PublicAccess");
  ```

### Make lease duration/break period a long
Lease Duration/Break Period are represented as a TimeSpan in the .NET client libraries, but TimeSpan.MaxValue would overflow an int. Because of this, we are changing the 
type used in the BlobRestClient from an int to a long. This will allow values larger than int.MaxValue (e.g. TimeSpan.MaxValue) to be successfully passed on to the service layer. 
``` yaml
directive:
- from: swagger-document
  where: $.parameters.LeaseDuration
  transform: >
    $.format = "int64";
- from: swagger-document
  where: $.parameters.LeaseBreakPeriod
  transform: >
    $.format = "int64";
```

### Merge the PageBlob AccessTier type
``` yaml
directive:
- from: swagger-document
  where: $.parameters.PremiumPageBlobAccessTierOptional
  transform: >
    $["x-ms-enum"].name = "AccessTier";
```

### Hide Result models relating to data lake
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?action=getAccessControl&directory"]
  transform: >
    $.head.responses["200"]["x-az-public"] = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?action=getAccessControl&blob"]
  transform: >
    $.head.responses["200"]["x-az-public"] = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?action=setAccessControl&directory"]
  transform: >
    $.patch.responses["200"]["x-az-public"] = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?action=setAccessControl&blob"]
  transform: >
    $.patch.responses["200"]["x-az-public"] = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?DirectoryRename"]
  transform: >
    $.put.responses["201"]["x-az-public"] = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?FileRename"]
  transform: >
    $.put.responses["201"]["x-az-public"] = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?resource=directory&Create"]
  transform: >
    $.put.responses["201"]["x-az-public"] = false;
- from: swagger-document
  where: $["x-ms-paths"]["/{filesystem}/{path}?DirectoryDelete"]
  transform: >
    $.delete.responses["200"]["x-az-public"] = false;
```

### Remove XMS prefix from ContentCrc64 property in Info models
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=appendblock&fromUrl"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=block"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=block&fromURL"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=blocklist"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=page&update"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=page&clear"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=page&update&fromUrl"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=appendblock"]
  transform: >
    $.put.responses["201"].headers["x-ms-content-crc64"]["x-ms-client-name"] = "ContentCrc64";
```

### Rename SetTier to SetAccessTier
``` yaml
directive:
- from: swagger-document
  where: $["x-ms-paths"]["/{containerName}/{blob}?comp=tier"]
  transform: >
    $.put.operationId = "Blob_SetAccessTier";
```
