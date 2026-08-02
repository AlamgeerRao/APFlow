// ============================================================================
// AP Flow — WP-024: Logging, Monitoring & Application Insights
// Resource-group-scope module. Adds availability monitoring, an operations
// dashboard, and alert rules on top of the Application Insights component +
// Log Analytics workspace already provisioned by resources.bicep (WP-021)
// and wired into the API by WP-066 (AddApplicationInsightsTelemetry()).
//
// Alert design note: the /health/ready response has no per-component
// ("graph"/"blob"/"database") tag on its JSON body (docs/Backlog.md,
// "Component tags on health check responses" — flagged in WP-004, still
// open, untouched by anything since). Rather than wait on that gap, these
// alert rules distinguish failures by component using Application Insights'
// own AUTOMATIC dependency telemetry instead (dependencies.type/target),
// which already exists per-call with no code change required. The database
// rule additionally checks for a real HTTP 503 from /health/ready itself —
// per WP-004's ruling, database-down is the ONLY dependency that flips that
// endpoint's HTTP status (Graph/Blob outages stay Degraded/200 by design) -
// so a 503 there is an unambiguous "database Unhealthy" signal on its own,
// no dependency-table parsing needed for that part.
// ============================================================================
targetScope = 'resourceGroup'

param location string
param namePrefix string
param environmentName string

@description('Resource ID of the Application Insights component (WP-021/WP-066) that alert rules and availability tests target.')
param appInsightsId string

param apiAppServiceId string
param apiAppServiceUrl string
param webAppServiceId string
param webAppServiceUrl string

@description('Email address that receives alert notifications (application failures, database Unhealthy). Blob/Graph rules deliberately have no action group — see module doc comment.')
param alertEmail string

var baseName = '${namePrefix}-${environmentName}'

// ----------------------------------------------------------------------------
// Availability (ping) tests — one per App Service, Standard test type.
// API pings /health/ready (a real reachability + database signal, not just
// "process is up" — WP-004's ruling means a 503 here specifically means the
// database is unreachable, matching this module's "database Unhealthy" alert
// below). The Web app has no server-side health endpoint (server.js is a
// static file server with no dependencies of its own - confirmed by reading
// it), so its test pings the SPA's own root document instead.
// ----------------------------------------------------------------------------
// 'emea-gb-db-azr' was tried first (assumed UK South) and rejected by Azure
// at deploy time as "not a supported location" - these two are confirmed
// valid via a real deployment.
var availabilityLocations = [
  { Id: 'emea-nl-ams-azr' } // West Europe
  { Id: 'emea-ru-msa-edge' } // Azure-hosted EMEA edge location
]

resource apiAvailabilityTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: 'avail-${baseName}-api'
  location: location
  tags: {
    'hidden-link:${appInsightsId}': 'Resource'
  }
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'avail-${baseName}-api'
    Name: 'AP Flow API - /health/ready'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: availabilityLocations
    Request: {
      RequestUrl: '${apiAppServiceUrl}/health/ready'
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      // Only a full outage/database-Unhealthy (503) or a genuine crash should
      // fail this test - Degraded (Graph/Blob down) still returns 200 by
      // design (WP-004), so this test correctly does NOT treat that as down.
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

resource webAvailabilityTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: 'avail-${baseName}-web'
  location: location
  tags: {
    'hidden-link:${appInsightsId}': 'Resource'
  }
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'avail-${baseName}-web'
    Name: 'AP Flow Web - /'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: availabilityLocations
    Request: {
      RequestUrl: webAppServiceUrl
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

// ----------------------------------------------------------------------------
// Action group — email only, dev-scale. Deliberately NOT attached to the
// Blob/Graph alert rules below (see their own comments) - only Application
// failures and Database Unhealthy actually notify.
// ----------------------------------------------------------------------------
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${baseName}-ops'
  location: 'global'
  properties: {
    groupShortName: 'apflowops'
    enabled: true
    emailReceivers: [
      {
        name: 'primary-email'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// ----------------------------------------------------------------------------
// Alert rules (Application Insights log alerts, scheduled query rules v2).
// All four scopes are distinguishable via App Insights per this module's
// own doc comment, regardless of the still-open component-tagging gap.
// ----------------------------------------------------------------------------

// Application failures - any server-side (5xx) failure across the API, not
// specific to one endpoint. Threshold of 3 in a 5-minute window (not 1) is
// NOT a workaround for the known post-deploy cold-start blip - that blip's
// own failing requests never reach Application Insights at all (confirmed
// live during this WP's investigation: zero `requests` rows for either the
// 404 or the 500 that occurred during WP-075's deploy, despite everything
// else in the same session being captured normally) - so this rule
// structurally cannot fire on that specific pattern regardless of threshold.
// The threshold of 3 is instead plain noise tolerance against one-off
// transient failures unrelated to a deploy.
resource applicationFailuresAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-${baseName}-application-failures'
  location: location
  kind: 'LogAlert'
  properties: {
    displayName: 'AP Flow - Application failures'
    description: 'Server-side (5xx) request failures on the API.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'requests | where success == "False"'
          timeAggregation: 'Count'
          operator: 'GreaterThanOrEqual'
          threshold: 3
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
    autoMitigate: true
  }
}

// Database Unhealthy - THE hard-blocking alert (task's own wording). Fires
// on either signal: /health/ready returning a genuine 503 (per WP-004's
// ruling, the ONLY dependency that flips that endpoint's HTTP status is the
// database - Graph/Blob stay Degraded/200), or a raw failed SQL dependency
// call captured automatically by the App Insights SDK. Threshold of 1 (not
// 3, unlike the application-failures rule above) - a single database outage
// is exactly the "hard-blocking" case this alert exists for; it should not
// wait for repetition.
resource databaseUnhealthyAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-${baseName}-database-unhealthy'
  location: location
  kind: 'LogAlert'
  properties: {
    displayName: 'AP Flow - Database Unhealthy (hard-blocking)'
    description: 'The database is unreachable: either /health/ready returned a real 503 (WP-004: the only dependency that does this), or a SQL dependency call failed.'
    severity: 1
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'union requests, dependencies | where (itemType == "request" and name == "GET /health/ready" and resultCode == "503") or (itemType == "dependency" and type == "SQL" and success == "False")'
          timeAggregation: 'Count'
          operator: 'GreaterThanOrEqual'
          threshold: 1
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
    autoMitigate: true
  }
}

// Blob Storage failures - exists and is fully queryable/distinguishable (the
// WP's own requirement), but deliberately has NO action group. Per WP-004's
// ruling (docs/WP-004-Health-Check-Severity-Decision.md) and this WP's own
// instruction, Blob being Degraded in dev is expected, not incident-worthy -
// this rule's history is visible in Azure Monitor for anyone who looks, it
// just does not page anyone.
resource blobFailuresAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-${baseName}-blob-failures'
  location: location
  kind: 'LogAlert'
  properties: {
    displayName: 'AP Flow - Blob Storage dependency failures (informational)'
    description: 'Failed Blob Storage dependency calls. Expected/not alertable in dev per WP-004 - no action group attached.'
    severity: 4
    enabled: true
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          // Not a real endpoint reference - a KQL text-match filter against
          // dependency target strings App Insights already recorded, so
          // environment()-based templating does not apply here.
          #disable-next-line no-hardcoded-env-urls
          query: 'dependencies | where target has "blob.core.windows.net" and success == "False"'
          timeAggregation: 'Count'
          operator: 'GreaterThanOrEqual'
          threshold: 1
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: []
    }
    autoMitigate: true
  }
}

// Graph failures - same reasoning as Blob above: distinguishable, no action group.
resource graphFailuresAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-${baseName}-graph-failures'
  location: location
  kind: 'LogAlert'
  properties: {
    displayName: 'AP Flow - Graph dependency failures (informational)'
    description: 'Failed Microsoft Graph dependency calls. Expected/not alertable in dev per WP-004 - no action group attached.'
    severity: 4
    enabled: true
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'dependencies | where target has "graph.microsoft.com" and success == "False"'
          timeAggregation: 'Count'
          operator: 'GreaterThanOrEqual'
          threshold: 1
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: []
    }
    autoMitigate: true
  }
}

// IngestionIssue (WP-076, optional per this task's own "if it has shipped by
// the time this runs" clause - it has). Informational only, no action group:
// this is a data-quality signal ("emails are arriving that can't be
// processed"), not an infrastructure incident - it does not belong on the
// same footing as a database outage. Threshold of 5 in an hour is a light
// noise filter - a single flagged email is normal/expected (that is the
// whole point of WP-076's feature), a sudden burst is worth a human glance.
resource ingestionIssueAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-${baseName}-ingestion-issue-burst'
  location: location
  kind: 'LogAlert'
  properties: {
    displayName: 'AP Flow - IngestionIssue burst (informational)'
    description: 'More than 5 unprocessable-email warnings (WP-076 IngestionIssue) logged in an hour - worth a look, not an incident.'
    severity: 4
    enabled: true
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'traces | where message has "had no processable PDF attachment"'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: []
    }
    autoMitigate: true
  }
}

// ----------------------------------------------------------------------------
// Dashboard - health (availability test results), request rate, failure
// rate (per App Service, both plotted on the same tile), plus one shared
// dependency-latency tile (App Insights - only the API makes outbound
// dependency calls; the Web app is a static file host with none of its own).
// NOTE: MonitorChartPart's inner chart-visualization fields (aggregation
// type / chart type codes) are internal Azure Portal implementation detail,
// not a published schema - this follows the well-established pattern from
// Microsoft's own quickstart-templates samples, but has not been visually
// confirmed rendering correctly (no browser tool available this session).
// Recommend opening the dashboard once after deploy to confirm tiles render;
// straightforward to adjust if any tile shows no data.
// ----------------------------------------------------------------------------
resource dashboard 'Microsoft.Portal/dashboards@2022-12-01-preview' = {
  name: 'dash-${baseName}-ops'
  location: location
  tags: {
    'hidden-title': 'AP Flow - Operations (${environmentName})'
  }
  properties: {
    lenses: [
      {
        order: 0
        parts: [
          {
            position: { x: 0, y: 0, rowSpan: 2, colSpan: 6 }
            metadata: {
              type: 'Extension/HubsExtension/PartType/MarkdownPart'
              settings: {
                content: {
                  settings: {
                    content: '# AP Flow — Operations (${environmentName})\n\nHealth (availability test results), request rate, failure rate, and dependency latency for both App Services. See [WP-024 report] for the recurring post-deploy blip root cause (Basic-tier cold container restart on every deploy, no deployment slot) and alert-rule design.'
                    title: ''
                    subtitle: ''
                  }
                }
              }
            }
          }
          {
            position: { x: 6, y: 0, rowSpan: 4, colSpan: 6 }
            metadata: {
              // BCP036/BCP037 below are confirmed cosmetic - Bicep's type
              // model for Microsoft.Portal/dashboards infers the `parts`
              // array's element type from the first part (a MarkdownPart)
              // rather than discriminating per-element by `type`, so every
              // subsequent MonitorChartPart is flagged against the wrong
              // shape. Verified by inspecting the compiled ARM JSON directly
              // (`az bicep build --stdout`): `type` and `settings.content`
              // both retain the correct MonitorChartPart shape - see this
              // module's own doc comment.
              #disable-next-line BCP036
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: []
              settings: {
                content: {
                  #disable-next-line BCP037
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: appInsightsId }
                          name: 'availabilityResults/availabilityPercentage'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: { displayName: 'Availability %' }
                        }
                      ]
                      title: 'Health - availability %'
                      titleKind: 1
                      visualization: {
                        chartType: 2
                        legendVisualization: { isVisible: true, position: 2, hideSubtitle: false }
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: { x: 0, y: 2, rowSpan: 4, colSpan: 6 }
            metadata: {
              // BCP036/BCP037 below are confirmed cosmetic - Bicep's type
              // model for Microsoft.Portal/dashboards infers the `parts`
              // array's element type from the first part (a MarkdownPart)
              // rather than discriminating per-element by `type`, so every
              // subsequent MonitorChartPart is flagged against the wrong
              // shape. Verified by inspecting the compiled ARM JSON directly
              // (`az bicep build --stdout`): `type` and `settings.content`
              // both retain the correct MonitorChartPart shape - see this
              // module's own doc comment.
              #disable-next-line BCP036
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: []
              settings: {
                content: {
                  #disable-next-line BCP037
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: apiAppServiceId }
                          name: 'Requests'
                          aggregationType: 1
                          namespace: 'microsoft.web/sites'
                          metricVisualization: { displayName: 'API - Requests' }
                        }
                        {
                          resourceMetadata: { id: webAppServiceId }
                          name: 'Requests'
                          aggregationType: 1
                          namespace: 'microsoft.web/sites'
                          metricVisualization: { displayName: 'Web - Requests' }
                        }
                      ]
                      title: 'Request rate (both App Services)'
                      titleKind: 1
                      visualization: {
                        chartType: 2
                        legendVisualization: { isVisible: true, position: 2, hideSubtitle: false }
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: { x: 6, y: 4, rowSpan: 4, colSpan: 6 }
            metadata: {
              // BCP036/BCP037 below are confirmed cosmetic - Bicep's type
              // model for Microsoft.Portal/dashboards infers the `parts`
              // array's element type from the first part (a MarkdownPart)
              // rather than discriminating per-element by `type`, so every
              // subsequent MonitorChartPart is flagged against the wrong
              // shape. Verified by inspecting the compiled ARM JSON directly
              // (`az bicep build --stdout`): `type` and `settings.content`
              // both retain the correct MonitorChartPart shape - see this
              // module's own doc comment.
              #disable-next-line BCP036
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: []
              settings: {
                content: {
                  #disable-next-line BCP037
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: apiAppServiceId }
                          name: 'Http5xx'
                          aggregationType: 1
                          namespace: 'microsoft.web/sites'
                          metricVisualization: { displayName: 'API - Http5xx' }
                        }
                        {
                          resourceMetadata: { id: webAppServiceId }
                          name: 'Http5xx'
                          aggregationType: 1
                          namespace: 'microsoft.web/sites'
                          metricVisualization: { displayName: 'Web - Http5xx' }
                        }
                      ]
                      title: 'Failure rate - Http5xx (both App Services)'
                      titleKind: 1
                      visualization: {
                        chartType: 2
                        legendVisualization: { isVisible: true, position: 2, hideSubtitle: false }
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: { x: 0, y: 6, rowSpan: 4, colSpan: 6 }
            metadata: {
              // BCP036/BCP037 below are confirmed cosmetic - Bicep's type
              // model for Microsoft.Portal/dashboards infers the `parts`
              // array's element type from the first part (a MarkdownPart)
              // rather than discriminating per-element by `type`, so every
              // subsequent MonitorChartPart is flagged against the wrong
              // shape. Verified by inspecting the compiled ARM JSON directly
              // (`az bicep build --stdout`): `type` and `settings.content`
              // both retain the correct MonitorChartPart shape - see this
              // module's own doc comment.
              #disable-next-line BCP036
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: []
              settings: {
                content: {
                  #disable-next-line BCP037
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: appInsightsId }
                          name: 'dependencies/duration'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: { displayName: 'Dependency duration (avg, ms)' }
                        }
                      ]
                      title: 'Dependency latency (SQL / Graph / Blob - API only)'
                      titleKind: 1
                      visualization: {
                        chartType: 2
                        legendVisualization: { isVisible: true, position: 2, hideSubtitle: false }
                      }
                    }
                  }
                }
              }
            }
          }
        ]
      }
    ]
    metadata: {
      model: {
        timeRange: {
          value: { relative: { duration: 24, timeUnit: 1 } }
          type: 'MsPortalFx.Composition.Configuration.ValueTypes.TimeRange'
        }
      }
    }
  }
}

output actionGroupId string = actionGroup.id
output apiAvailabilityTestName string = apiAvailabilityTest.name
output webAvailabilityTestName string = webAvailabilityTest.name
output dashboardName string = dashboard.name
