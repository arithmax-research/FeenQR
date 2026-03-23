import os
import requests
from data_pipeline.env_loader import load_env_file
from dotenv import load_dotenv
load_dotenv()
load_env_file()

def get_quantconnect_docs():
    """Extract relevant excerpts from QuantConnect documentation PDF."""
    try:
        from PyPDF2 import PdfReader
        pdf_path = "/home/misango/codechest/LeanBacktester/Quant-Connect-documentation.pdf"
        reader = PdfReader(pdf_path)
        
        docs = ""
        for page in reader.pages[:10]:
            docs += page.extract_text() + "\n"
        for page in reader.pages[190:250]:
            docs += page.extract_text() + "\n"
        return docs[:5000]
    except Exception as e:
        return f"Error loading docs: {e}"

QUANTCONNECT_DOCS = get_quantconnect_docs()

class DeepSeekCoder:
    def __init__(self):
        self.api_key = os.getenv('DEEPSEEK_API_KEY')
        if not self.api_key:
            raise ValueError("DEEPSEEK_API_KEY not found in environment variables")
        self.base_url = "https://api.deepseek.com/v1/chat/completions"

    def generate_code(self, prompt, model=None, system_prompt=None):
        model = model or "deepseek-coder"
        messages = []
        if system_prompt:
            messages.append({"role": "system", "content": system_prompt})
        messages.append({"role": "user", "content": prompt})
        data = {
            "model": model,
            "messages": messages,
            "temperature": 0.3,
            "max_tokens": 4000
        }
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json"
        }
        response = requests.post(self.base_url, headers=headers, json=data, timeout=300)
        if response.status_code == 200:
            result = response.json()
            return result["choices"][0]["message"]["content"]
        else:
            raise Exception(f"DeepSeek API call failed: {response.status_code} - {response.text}")

def generate_strategy_code(strategy_text, example_code=""):
    """Generate C# strategy code from text description."""
    coder = DeepSeekCoder()
    system_prompt = "You are an expert C# developer for QuantConnect/LEAN algorithms. Generate complete, runnable C# code based on strategy descriptions. Always provide the full code without truncation. NEVER use markdown code blocks or backticks - return only raw C# code that can be directly compiled."
    user_prompt = f"""
Based on the following strategy description, generate a complete QuantConnect/LEAN C# algorithm class.

Strategy Description:
{strategy_text}

Use this format and structure similar to existing strategies. The class should inherit from QCAlgorithm.

Include:
- Initialize method with date ranges, cash, symbols
- Indicators as needed (use correct QuantConnect types: RelativeStrengthIndex for RSI, ExponentialMovingAverage for EMA, AverageTrueRange for ATR, IntradayVwap for VWAP, etc.)
- OnData method for trading logic
- Proper risk management
- Logging

Reference QuantConnect Documentation:
{QUANTCONNECT_DOCS[:1000]}

Important: Use the correct QuantConnect namespaces and types. Include necessary using directives at the top.

Example structure:
{example_code}

IMPORTANT: Return ONLY the raw C# code without any markdown formatting, code blocks, or explanations. Start directly with the using statements and namespace declaration.
"""
    code = coder.generate_code(user_prompt, system_prompt=system_prompt)
    code = clean_code_response(code)
    return code

def clean_code_response(code):
    if "```python" in code:
        start = code.find("```python") + 9
        end = code.find("```", start)
        if end > start:
            code = code[start:end].strip()
    elif "```csharp" in code:
        start = code.find("```csharp") + 9
        end = code.find("```", start)
        if end > start:
            code = code[start:end].strip()
    elif "```" in code:
        start = code.find("```") + 3
        end = code.find("```", start)
        if end > start:
            code = code[start:end].strip()

    code = code.replace("`", "")

    lines = code.split('\n')
    cleaned_lines = []
    for line in lines:
        line = line.strip()
        if line and not line.startswith('```'):
            cleaned_lines.append(line)

    return '\n'.join(cleaned_lines)

def get_python_example_code():
    """Get example Python code from existing strategy for reference."""
    from pathlib import Path
    example_path = Path(__file__).parent / "arithmax-strategies" / "DiversifiedLeverage_python" / "main.py"
    if example_path.exists():
        with open(example_path, 'r') as f:
            return f.read()[:1500]
    return ""

def generate_python_strategy_code(strategy_text, example_code=""):
    """Generate Python strategy code from text description."""
    coder = DeepSeekCoder()
    system_prompt = "You are an expert Python developer for QuantConnect/LEAN algorithms. Generate complete, runnable Python code based on strategy descriptions. Always provide the full code without truncation. NEVER use markdown code blocks or backticks - return only raw Python code that can be directly executed."
    user_prompt = f"""
Based on the following strategy description, generate a complete QuantConnect/LEAN Python algorithm class.

Strategy Description:
{strategy_text}

Use this format and structure similar to existing strategies. The class should inherit from QCAlgorithm.

Include:
- Import from AlgorithmImports at the top
- Class that inherits from QCAlgorithm
- Initialize method with date ranges, cash, symbols
- Indicators as needed (use correct QuantConnect Python types: RelativeStrengthIndex for RSI, ExponentialMovingAverage for EMA, etc.)
- OnData method for trading logic
- Proper risk management
- Logging using self.Log()

Reference QuantConnect Documentation:
{QUANTCONNECT_DOCS[:1000]}

Important: Use the correct QuantConnect Python API. Follow Python naming conventions (snake_case for variables, PascalCase for classes).

Example structure:
{example_code}

IMPORTANT: Return ONLY the raw Python code without any markdown formatting, code blocks, or explanations. Start directly with the imports and class definition.
"""
    code = coder.generate_code(user_prompt, system_prompt=system_prompt)
    code = clean_code_response(code)
    return code

def fix_compilation_errors(generated_code, compilation_errors, strategy_text):
    """Fix compilation errors in generated C# code."""
    coder = DeepSeekCoder()
    
    system_prompt = "You are an expert C# developer for QuantConnect/LEAN algorithms. Fix compilation errors in the provided code and return only the corrected C# code."
    user_prompt = f"""
Apply targeted fixes to the existing C# code based on the compilation errors. Do NOT regenerate the entire code from scratch—only modify the parts that are broken.

Original Strategy Description:
{strategy_text[:500]}

Existing Code with Errors:
{generated_code}

Compilation Errors:
{compilation_errors}

Reference QuantConnect Documentation:
{QUANTCONNECT_DOCS[:500]}

Fix only the specific errors mentioned. Use correct QuantConnect types and namespaces. Return the complete corrected C# code.
"""
    try:
        fixed_code = coder.generate_code(user_prompt, system_prompt=system_prompt)
        fixed_code = clean_code_response(fixed_code)
        return fixed_code
    except Exception as e:
        return f"Error in fixing compilation: {e}"

def generate_data_requirements_summary(generated_code, strategy_text, language='csharp'):
    """Generate a summary of data requirements from the generated strategy code."""
    coder = DeepSeekCoder()
    
    lang_name = "Python" if language == 'python' else "C#"
    system_prompt = f"Analyze the provided {lang_name} QuantConnect strategy code and strategy description to provide a concise JSON summary of data requirements."
    user_prompt = f"""
Analyze the following generated {lang_name} QuantConnect strategy code and the original strategy description to provide a concise summary of data requirements.

Generated Code:
{generated_code[:2000]}

Original Strategy Description:
{strategy_text[:1000]}

Provide a JSON-like summary with:
- symbols: List of stock/crypto symbols used
- start_date: Backtest start date (YYYY-MM-DD)
- end_date: Backtest end date (YYYY-MM-DD) 
- resolution: Data resolution (Daily/Minute/Hour)
- data_types: Types of data needed (equity, crypto, options, etc.)
- special_requirements: Any special data needs (earnings dates, volume spikes, etc.)

Format as a simple JSON object.
"""
    try:
        return coder.generate_code(user_prompt, system_prompt=system_prompt)
    except Exception as e:
        return f"Error generating data requirements: {e}"
