# Azure Billing Tag

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fwilfriedwoivre%2Fazure-billing-tag-generator%2Fmaster%2Fdeployments%2Fazuredeploy.json)

## Application

* Azure Function
  * Billing Tag Manager : Timer Trigger
  * Managed Service Identity

You must assign Managed Service Identity to your target subscription with role **Contributor** or custom role Write tags on ResourceGroup and can read billing data


## Billing Tag Manager

TimerTrigger with schedule : __0 0 */4 * * *__
