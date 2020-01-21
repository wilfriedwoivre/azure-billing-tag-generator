# Azure Sandbox

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://azuredeploy.net/?repository=https://github.com/wilfriedwoivre/azure-billing-tag-generator/tree/master/deployments)

## Application

* Azure Function
  * Billing Tag Manager : Timer Trigger
  * Managed Service Identity

You must assign Managed Service Identity to your target subscription with role **Contributor** or custom role Write tags on ResourceGroup and can read billing data


## Billing Tag Manager

TimerTrigger with schedule : __0 0 */4 * * *__
