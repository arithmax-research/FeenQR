import asyncio
import yaml
import pandas as pd
from langchain_experimental.tools.python.tool import PythonAstREPLTool
from autogen_ext.tools.langchain import LangChainToolAdapter
from autogen_ext.models.openai import OpenAIChatCompletionClient
from autogen_agentchat.messages import TextMessage
from autogen_agentchat.agents import AssistantAgent
from autogen_agentchat.ui import Console
from autogen_core import CancellationToken


async def main() -> None:
    df = pd.read_csv("/workspaces/Quant_Research_Assistant/paper/historical_data.csv")  # type: ignore
    tool = LangChainToolAdapter(PythonAstREPLTool(locals={"df": df}))
    with open("model_config.yml", "r") as f:
        model_config = yaml.safe_load(f)
    model_client = OpenAIChatCompletionClient.load_component(model_config)
    agent = AssistantAgent(
        "assistant",
        tools=[tool],
        model_client=model_client,
        system_message="Use the `df` variable to access the dataset.",
    )
    await Console(
        agent.on_messages_stream(
            [TextMessage(content="Count duplicates in the dataset", source="user")], CancellationToken()
        )
    )


asyncio.run(main())