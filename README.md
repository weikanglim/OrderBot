# Introduction 
This repository contains chat bot implementations using Microsoft Bot Framework that performs orders for businesses.

# Getting Started
Recommended tooling:
- Azure Bot Framework Emulator
- Bot Builder tools
```
npm install -g chatdown msbot ludown luis-apis qnamaker botdispatch luisgen
```

See [ludown instructions](https://github.com/Microsoft/botbuilder-tools/blob/master/packages/Ludown/docs/create-luis-json.md)

# Build and Test
The bot currently depends on a LUIS app being deployed. You can use `ludown` to generate a json file containing the LUIS model. Upload the file to the [LUIS portal](https://www.luis.ai/) and fill in the bot settings in `order-chatbot.bot`.

Additional app settings are required for the bot to run. Read the `Startup.cs` files to figure out which settings are required.