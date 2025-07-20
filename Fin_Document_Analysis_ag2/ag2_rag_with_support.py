import asyncio
from autogen import AssistantAgent, UserProxyAgent, GroupChat, GroupChatManager
from dotenv import load_dotenv
load_dotenv()
import os
import nest_asyncio
from autogen.agents.experimental import DeepResearchAgent
import autogen
nest_asyncio.apply()
from typing_extensions import Annotated
from autogen.agentchat.contrib.retrieve_user_proxy_agent import RetrieveUserProxyAgent
import chromadb
from typing_extensions import Annotated

config_list = autogen.config_list_from_json("OAI_CONFIG_LIST.json")

print("LLM models: ", [config_list[i]["model"] for i in range(len(config_list))])


gpt4o_config = {
    "model": "gpt-4o-mini",
    "api_key": os.environ.get("OPEN_AI_API"),
}

llm_config = {
    "timeout": 60,
    "api_type": "openai",
    "model": "gpt-4o-mini",
    "api_key": os.environ.get("OPEN_AI_API"),
    "temperature": 0.8,
    "seed": 1234
}


def termination_msg(x):
    return isinstance(x, dict) and "TERMINATE" == str(x.get("content", ""))[-9:].upper()

boss = autogen.UserProxyAgent(
    name="SeniorQuant",
    is_termination_msg=termination_msg,
    system_message="You are a senior quant and you ask questions and give tasks.",
    human_input_mode="NEVER",
    code_execution_config=False, 
    description="The boss who ask questions and give tasks.",
)

boss_aid = RetrieveUserProxyAgent(
    name="QuantAssistant",
    is_termination_msg=termination_msg,
    human_input_mode="NEVER",
    max_consecutive_auto_reply=3,
    retrieve_config={
        "task": "code",
        "docs_path": "Financial_Documents/Successful Algorithmic Trading.pdf",
        "chunk_token_size": 1000,
        "model": "gpt-4o",
        "collection_name": "groupchat",
        "get_or_create": True,
    },
    code_execution_config={
        "last_n_messages": 3,
        "work_dir": "paper",
        "use_docker": True
    },
    description="Assistant who has extra content retrieval power for solving difficult problems.",
)

coder = AssistantAgent(
    name="SeniorPythonEngineer",
    is_termination_msg=termination_msg,
    system_message="""You are a senior python engineer and you provide python code to answer questions. 
    Wrap the code in a code block that specifies the script type. The user can't modify your code. Don't include multiple code blocks in one response.\
    Do not ask others to copy and paste the result. Check the execution result returned by the executor.\
    If the result indicates there is an error, fix the error and output the code again. Suggest the full code instead of partial code or code changes.\
    If the error can't be fixed or if the task is not solved even after the code is executed successfully, analyse the problem.
    For Graph generation, use matplotlib/sns and make sure there are buy/sell signals as cones and coloring to indicate bear/bull trends .\
    For PDF generation, There should be no huge whitespaces between texts. Make it compact and readable\with all images, code used, tables and graphss included.',""",
    llm_config=llm_config,
    description="Senior Python Engineer who can write code to solve problems and answer questions.",
)


pm = autogen.AssistantAgent(
    name="ProductManager",
    is_termination_msg=termination_msg,
    llm_config=llm_config,
    description="Product Manager who can design and plan the project.",
)

reviewer = autogen.AssistantAgent(
    name="CodeReviewer",
    is_termination_msg=termination_msg,
    llm_config=llm_config,
    description="Code Reviewer who can review the code.",
)

research_report_writer = AssistantAgent(
    name='ResearchWriter',
    system_message='Research Report Writer. Write a research report based on the findings from the papers categorized by the scientist and exchange with critic to improve the quality of the report.\
    The report should include the following sections: Title, Introduction, Literature Review, Methodology, Results, Conclusion, and References.\
    The report should have a subtitle with :  Produced by Frankline&CoLP Quant Research AI Assstant.\
    The report should be written in a clear and concise manner. Make sure to include proper citation and references.\
    Ask the Engineer to generate graphs and tables for the report. The report should be saved as a PDF file. \
    The engineer can run code to save the pdf file. The report should be saved as a PDF file.',
    llm_config=gpt4o_config
)

def _reset_agents():
    boss.reset()
    boss_aid.reset()
    coder.reset()
    pm.reset()
    reviewer.reset()

def rag_chat():
    _reset_agents()
    groupchat = autogen.GroupChat(
        agents=[boss, boss_aid, pm, coder, reviewer], messages=[], max_round=12, speaker_selection_method="round_robin"
    )
    manager = autogen.GroupChatManager(groupchat=groupchat, llm_config=llm_config)

    # Start chatting with boss_aid as this is the user proxy agent.
    boss_aid.initiate_chat(
        manager,
        message=boss_aid.message_generator,
        problem=PROBLEM,
        n_results=3,
    )


def norag_chat():
    _reset_agents()
    groupchat = autogen.GroupChat(
        agents=[boss, pm, coder, reviewer, research_report_writer],
        messages=[],
        max_round=10,
        speaker_selection_method="auto",
        allow_repeat_speaker=False,
    )
    manager = autogen.GroupChatManager(groupchat=groupchat, llm_config=llm_config)

    # Start chatting with the boss as this is the user proxy agent.
    boss.initiate_chat(
        manager,
        message=PROBLEM,
    )


def call_rag_chat():
    _reset_agents()

    # In this case, we will have multiple user proxy agents and we don't initiate the chat
    # with RAG user proxy agent.
    # In order to use RAG user proxy agent, we need to wrap RAG agents in a function and call
    # it from other agents.
    def retrieve_content(
        message: Annotated[
            str,
            "Refined message which keeps the original meaning and can be used to retrieve content for code generation and question answering.",
        ],
        n_results: Annotated[int, "number of results"] = 3,) -> str:
        
        boss_aid.n_results = n_results  # Set the number of results to be retrieved.
        _context = {"problem": message, "n_results": n_results}
        ret_msg = boss_aid.message_generator(boss_aid, None, _context)
        return ret_msg or message

    boss_aid.human_input_mode = "NEVER"  # Disable human input for boss_aid since it only retrieves content.

    for caller in [pm, coder, reviewer]:
        d_retrieve_content = caller.register_for_llm(
            description="retrieve content for code generation and question answering.", api_style="function"
        )(retrieve_content)

    for executor in [boss, pm]:
        executor.register_for_execution()(d_retrieve_content)

    groupchat = autogen.GroupChat(
        agents=[boss, boss_aid, pm, coder, reviewer, research_report_writer],
        messages=[],
        max_round=10,
        speaker_selection_method="round_robin",
        allow_repeat_speaker=False,
    )

    manager = autogen.GroupChatManager(groupchat=groupchat, llm_config=llm_config)

    # Start chatting with the boss as this is the user proxy agent.
    boss.initiate_chat(
        manager,
        message=PROBLEM,
    )

PROBLEM = "Write a report and Analyze Nvidia stock using 2025 data"

norag_chat()